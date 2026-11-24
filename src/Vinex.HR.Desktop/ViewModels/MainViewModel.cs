using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Vinex.HR.Desktop.Core;

namespace Vinex.HR.Desktop.ViewModels;

/// <summary>Root view model: shows the login page, the forced password-change page, or the application shell.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly SessionState _session;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private object? _current;

    public MainViewModel(SessionState session, IServiceProvider services)
    {
        _session = session;
        _services = services;
        _session.SignedIn += () => RunOnUi(ShowAfterSignIn);
        _session.SignedOut += reason => RunOnUi(() => ShowLogin(reason));
        ShowLogin(null);
    }

    private void ShowLogin(string? message)
    {
        var login = _services.GetRequiredService<LoginViewModel>();
        login.Notice = message;
        Current = login;
    }

    private void ShowAfterSignIn()
    {
        if (_session.User?.MustChangePassword == true)
        {
            var change = _services.GetRequiredService<ChangePasswordViewModel>();
            change.IsForced = true;
            Current = change;
        }
        else
        {
            Current = _services.GetRequiredService<ShellViewModel>();
        }
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }
}
