using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Vinex.HR.Desktop.Core;
using Vinex.HR.Desktop.Services;
using Vinex.HR.Desktop.ViewModels;
using Vinex.HR.Desktop.Views;

namespace Vinex.HR.Desktop;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception");
            MessageBox.Show("An unexpected error occurred. Details were written to the log.", "Vinex HR",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vinex", "HR", "logs", "desktop-.log");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
                .CreateLogger();

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = AppContext.BaseDirectory,
                Args = e.Args,
            });
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(Log.Logger, dispose: true);
            ConfigureServices(builder.Services);

            _host = builder.Build();
            await _host.StartAsync();

            _host.Services.GetRequiredService<IdleMonitor>().Start();
            var window = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup failed");
            MessageBox.Show($"Vinex HR could not start: {ex.Message}", "Vinex HR", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<SessionState>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IdleMonitor>();

        services.AddTransient<AuthHeaderHandler>();
        services.AddHttpClient<AnonymousApiClient>(ConfigureClient);
        services.AddHttpClient<ApiClient>(ConfigureClient).AddHttpMessageHandler<AuthHeaderHandler>();

        services.AddTransient<AuthApi>();
        services.AddTransient<UsersApi>();

        services.AddSingleton<MainViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<UserEditorViewModel>();
        services.AddTransient<ResetPasswordViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private static void ConfigureClient(IServiceProvider sp, HttpClient client)
    {
        var baseUrl = sp.GetRequiredService<IConfiguration>()["Api:BaseUrl"] ?? "https://localhost:5001/";
        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    }
}
