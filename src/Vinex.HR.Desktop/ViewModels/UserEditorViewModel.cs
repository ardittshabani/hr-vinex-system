using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vinex.HR.Contracts.Security;
using Vinex.HR.Contracts.Users;
using Vinex.HR.Contracts.Validators;
using Vinex.HR.Desktop.Core;
using Vinex.HR.Desktop.Services;

namespace Vinex.HR.Desktop.ViewModels;

public sealed record RoleOption(string Name, string DisplayName);

public sealed partial class UserEditorViewModel(UsersApi api) : ViewModelBase, IDialogViewModel
{
    private static readonly CreateUserRequestValidator CreateValidator = new();
    private static readonly UpdateUserRequestValidator UpdateValidator = new();

    private UserDto? _existing;

    public event Action<bool?>? CloseRequested;

    public IReadOnlyList<RoleOption> Roles { get; private set; } = [];

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private RoleOption? _selectedRole;

    [ObservableProperty]
    private string _initialPassword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEdit))]
    private bool _isCreate = true;

    public bool IsEdit => !IsCreate;

    public async Task InitializeAsync(UserDto? existing)
    {
        _existing = existing;
        IsCreate = existing is null;
        Roles = RoleNames.All.Select(r => new RoleOption(r, RoleNames.Display(r))).ToList();
        OnPropertyChanged(nameof(Roles));

        if (existing is not null)
        {
            Username = existing.Username;
            DisplayName = existing.DisplayName;
            Email = existing.Email ?? string.Empty;
            SelectedRole = Roles.FirstOrDefault(r => r.Name == existing.Role);
        }
        else
        {
            SelectedRole = Roles.FirstOrDefault(r => r.Name == RoleNames.Management);
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var role = SelectedRole?.Name ?? string.Empty;
        var email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();

        if (_existing is null)
        {
            var request = new CreateUserRequest(Username.Trim(), DisplayName.Trim(), email, role, InitialPassword);
            if (!Validate(CreateValidator, request))
            {
                return;
            }

            await RunAsync(async () =>
            {
                await api.CreateAsync(request);
                CloseRequested?.Invoke(true);
            });
        }
        else
        {
            var request = new UpdateUserRequest(DisplayName.Trim(), email, role, _existing.RowVersion);
            if (!Validate(UpdateValidator, request))
            {
                return;
            }

            await RunAsync(async () =>
            {
                await api.UpdateAsync(_existing.Id, request);
                CloseRequested?.Invoke(true);
            });
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}

public sealed partial class ResetPasswordViewModel(UsersApi api) : ViewModelBase, IDialogViewModel
{
    private static readonly ResetPasswordRequestValidator Validator = new();

    public event Action<bool?>? CloseRequested;

    public UserDto? Target { get; set; }

    public string Heading => $"Set a new temporary password for {Target?.DisplayName}. They must change it at next sign-in.";

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Target is null || !Validate(Validator, new ResetPasswordRequest(NewPassword)))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await api.ResetPasswordAsync(Target.Id, NewPassword);
            CloseRequested?.Invoke(true);
        });
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}
