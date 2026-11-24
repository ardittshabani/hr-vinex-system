using System.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentValidation;
using System.ComponentModel;

namespace Vinex.HR.Desktop.Core;

/// <summary>
/// Base for view models: busy/error state, <see cref="INotifyDataErrorInfo"/> backed by the same FluentValidation
/// validators the API uses, and mapping of server-side validation errors onto fields.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;

    public bool IsNotBusy => !IsBusy;
    public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);

    // ---- INotifyDataErrorInfo ----
    public bool HasErrors => _errors.Count > 0;
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName is not null && _errors.TryGetValue(propertyName, out var list) ? list : Array.Empty<string>();

    /// <summary>Validates with a FluentValidation validator and publishes field errors. Returns true when valid.</summary>
    protected bool Validate<T>(IValidator<T> validator, T instance)
    {
        ClearFieldErrors();
        var result = validator.Validate(instance);
        foreach (var group in result.Errors.GroupBy(e => e.PropertyName))
        {
            AddFieldErrors(group.Key, group.Select(e => e.ErrorMessage));
        }

        return result.IsValid;
    }

    protected void AddFieldErrors(string propertyName, IEnumerable<string> messages)
    {
        var target = GetType().GetProperty(propertyName) is not null ? propertyName : null;
        if (target is null)
        {
            // Not a field on this view model: show it as a general message.
            ErrorMessage = string.Join(" ", messages);
            return;
        }

        _errors[target] = messages.ToList();
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(target));
        OnPropertyChanged(nameof(HasErrors));
    }

    protected void ClearFieldErrors()
    {
        var keys = _errors.Keys.ToList();
        _errors.Clear();
        foreach (var key in keys)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(key));
        }

        OnPropertyChanged(nameof(HasErrors));
    }

    protected void ApplyServerErrors(ApiException ex)
    {
        ClearFieldErrors();
        ErrorMessage = null;
        if (ex.Errors is { Count: > 0 })
        {
            foreach (var (field, messages) in ex.Errors)
            {
                AddFieldErrors(field, messages);
            }

            if (!HasErrors && string.IsNullOrEmpty(ErrorMessage))
            {
                ErrorMessage = ex.Message;
            }
        }
        else
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>Runs an async action with busy state and uniform error handling.</summary>
    protected async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (ApiException ex)
        {
            ApplyServerErrors(ex);
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Unexpected error in {ViewModel}", GetType().Name);
            ErrorMessage = "Something went wrong. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
