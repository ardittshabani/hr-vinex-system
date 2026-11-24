using Microsoft.Extensions.Configuration;
using System.Windows.Input;
using System.Windows.Threading;

namespace Vinex.HR.Desktop.Core;

/// <summary>Signs the user out after a period without keyboard/mouse activity.</summary>
public sealed class IdleMonitor
{
    private readonly SessionState _session;
    private readonly TimeSpan _timeout;
    private readonly DispatcherTimer _timer;
    private DateTime _lastActivity = DateTime.UtcNow;

    public IdleMonitor(SessionState session, IConfiguration configuration)
    {
        _session = session;
        var minutes = configuration.GetValue("Security:IdleTimeoutMinutes", 30);
        _timeout = TimeSpan.FromMinutes(Math.Max(1, minutes));
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => Check();
    }

    public void Start()
    {
        InputManager.Current.PreProcessInput += (_, e) =>
        {
            if (e.StagingItem.Input is MouseEventArgs or KeyboardEventArgs)
            {
                _lastActivity = DateTime.UtcNow;
            }
        };
        _timer.Start();
    }

    private void Check()
    {
        if (_session.IsSignedIn && DateTime.UtcNow - _lastActivity > _timeout)
        {
            _session.SignOut("You were signed out after a period of inactivity.");
        }
    }
}
