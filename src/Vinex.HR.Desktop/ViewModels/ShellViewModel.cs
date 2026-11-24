using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Vinex.HR.Contracts.Security;
using Vinex.HR.Desktop.Core;
using Vinex.HR.Desktop.Services;

namespace Vinex.HR.Desktop.ViewModels;

public sealed record NavItem(string Title, string Glyph, string? Permission, Func<object> Create);

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly SessionState _session;
    private readonly AuthApi _auth;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private NavItem? _selectedItem;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private string _pageTitle = string.Empty;

    public IReadOnlyList<NavItem> NavigationItems { get; }
    public string UserName { get; }
    public string RoleText { get; }
    public string TodayText => DateTime.Now.ToString("dddd, d MMMM yyyy");

    public ShellViewModel(SessionState session, AuthApi auth, IDialogService dialogs, IServiceProvider services)
    {
        _session = session;
        _auth = auth;
        _dialogs = dialogs;
        _services = services;

        UserName = session.User?.DisplayName ?? string.Empty;
        RoleText = RoleNames.Display(session.User?.Role ?? string.Empty);

        // Glyphs are Segoe MDL2 Assets code points.
        var all = new List<NavItem>
        {
            new("Dashboard", "", Permissions.DashboardView, () => _services.GetRequiredService<DashboardViewModel>()),
            new("Employees", "", Permissions.EmployeesView, () => new ComingSoonViewModel("Employees", "Phase 2", "Employee registration, profiles, contracts, documents and employment history.")),
            new("Departments", "", Permissions.DepartmentsView, () => new ComingSoonViewModel("Departments", "Phase 2", "Manage departments and job positions.")),
            new("Attendance", "", Permissions.AttendanceView, () => new ComingSoonViewModel("Attendance", "Phase 3", "Daily attendance sheet, corrections with audit trail, monthly calendar and working-hours calculations.")),
            new("Shifts", "", Permissions.ShiftsView, () => new ComingSoonViewModel("Shifts", "Phase 4", "Shift definitions, working and rest days, and employee shift assignments.")),
            new("Leave Management", "", Permissions.LeaveView, () => new ComingSoonViewModel("Leave Management", "Phase 4", "Leave types, balances, registration and the leave calendar.")),
            new("Reports", "", Permissions.ReportsView, () => new ComingSoonViewModel("Reports", "Phase 5", "Employee, department, attendance, absence, overtime and leave reports with Excel and PDF export.")),
            new("User Management", "", Permissions.UsersManage, () => _services.GetRequiredService<UsersViewModel>()),
            new("Settings", "", Permissions.SettingsManage, () => new ComingSoonViewModel("Settings", "Phase 5", "Company settings, holidays and the audit log viewer.")),
        };

        NavigationItems = all.Where(i => i.Permission is null || session.Can(i.Permission)).ToList();
        SelectedItem = NavigationItems.FirstOrDefault();
    }

    partial void OnSelectedItemChanged(NavItem? value)
    {
        if (value is null)
        {
            return;
        }

        PageTitle = value.Title;
        CurrentPage = value.Create();
    }

    [RelayCommand]
    private void ChangePassword()
    {
        var vm = _services.GetRequiredService<ChangePasswordViewModel>();
        _dialogs.ShowDialog(vm, "Change password");
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        var refresh = _session.RefreshToken;
        try
        {
            await _auth.LogoutAsync(refresh);
        }
        catch (Exception)
        {
            // best effort: the session is cleared locally regardless
        }

        _session.SignOut();
    }
}
