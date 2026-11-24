using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vinex.HR.Contracts.Auth;
using Vinex.HR.Contracts.Validators;
using Vinex.HR.Desktop.Core;
using Vinex.HR.Desktop.Services;

namespace Vinex.HR.Desktop.ViewModels;

public sealed partial class LoginViewModel(AuthApi auth, SessionState session) : ViewModelBase
{
    private static readonly LoginRequestValidator Validator = new();

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    private string? _notice;

    public bool HasNotice => !string.IsNullOrEmpty(Notice);

    [RelayCommand]
    private async Task LoginAsync()
    {
        Notice = null;
        if (!Validate(Validator, new LoginRequest(Username, Password)))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var tokens = await auth.LoginAsync(Username.Trim(), Password);
            Password = string.Empty;
            session.SignIn(tokens);
        });
    }
}
