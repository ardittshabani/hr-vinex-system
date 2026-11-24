using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vinex.HR.Contracts.Auth;
using Vinex.HR.Contracts.Validators;
using Vinex.HR.Desktop.Core;
using Vinex.HR.Desktop.Services;

namespace Vinex.HR.Desktop.ViewModels;

/// <summary>Used both as a dialog (voluntary change) and as the full-page step forced at first sign-in.</summary>
public sealed partial class ChangePasswordViewModel(AuthApi auth, SessionState session) : ViewModelBase, IDialogViewModel
{
    private static readonly ChangePasswordRequestValidator Validator = new();

    public event Action<bool?>? CloseRequested;

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CancelText), nameof(Heading), nameof(Intro))]
    private bool _isForced;

    public string CancelText => IsForced ? "Sign out" : "Cancel";
    public string Heading => IsForced ? "Set a new password" : "Change password";
    public string Intro => IsForced
        ? "Your password was set by an administrator. Choose a new one to continue."
        : "Enter your current password and choose a new one (at least 10 characters).";

    [RelayCommand]
    private async Task SubmitAsync()
    {
        ErrorMessage = null;
        if (!Validate(Validator, new ChangePasswordRequest(CurrentPassword, NewPassword)))
        {
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            AddFieldErrors(nameof(ConfirmPassword), ["Passwords do not match."]);
            return;
        }

        await RunAsync(async () =>
        {
            var tokens = await auth.ChangePasswordAsync(CurrentPassword, NewPassword);
            if (IsForced)
            {
                session.SignIn(tokens); // moves from the forced page to the shell
            }
            else
            {
                session.UpdateTokens(tokens);
                CloseRequested?.Invoke(true);
            }
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsForced)
        {
            session.SignOut();
        }
        else
        {
            CloseRequested?.Invoke(false);
        }
    }
}
