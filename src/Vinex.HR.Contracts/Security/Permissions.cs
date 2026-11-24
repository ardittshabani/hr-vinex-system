namespace Vinex.HR.Contracts.Security;

/// <summary>Permission names used by API authorization policies and by the desktop UI to show/hide features.</summary>
public static class Permissions
{
    public const string DashboardView = "Dashboard.View";
    public const string EmployeesView = "Employees.View";
    public const string EmployeesViewSensitive = "Employees.ViewSensitive";
    public const string EmployeesManage = "Employees.Manage";
    public const string DepartmentsView = "Departments.View";
    public const string DepartmentsManage = "Departments.Manage";
    public const string AttendanceView = "Attendance.View";
    public const string AttendanceManage = "Attendance.Manage";
    public const string ShiftsView = "Shifts.View";
    public const string ShiftsManage = "Shifts.Manage";
    public const string LeaveView = "Leave.View";
    public const string LeaveManage = "Leave.Manage";
    public const string ReportsView = "Reports.View";
    public const string UsersManage = "Users.Manage";
    public const string SettingsManage = "Settings.Manage";
    public const string AuditView = "Audit.View";

    public static readonly IReadOnlyList<string> All =
    [
        DashboardView,
        EmployeesView, EmployeesViewSensitive, EmployeesManage,
        DepartmentsView, DepartmentsManage,
        AttendanceView, AttendanceManage,
        ShiftsView, ShiftsManage,
        LeaveView, LeaveManage,
        ReportsView,
        UsersManage, SettingsManage, AuditView,
    ];
}

public static class RoleNames
{
    public const string HRAdministrator = "HRAdministrator";
    public const string Management = "Management";

    public static readonly IReadOnlyList<string> All = [HRAdministrator, Management];

    public static string Display(string role) => role switch
    {
        HRAdministrator => "HR Administrator",
        Management => "Management",
        _ => role,
    };
}

/// <summary>Fixed role to permission mapping. Adding a role later means adding a case here.</summary>
public static class RolePermissions
{
    private static readonly HashSet<string> HrAdmin = new(Permissions.All);

    private static readonly HashSet<string> Management =
    [
        Permissions.DashboardView,
        Permissions.EmployeesView,
        Permissions.DepartmentsView,
        Permissions.AttendanceView,
        Permissions.ShiftsView,
        Permissions.LeaveView,
        Permissions.ReportsView,
    ];

    private static readonly HashSet<string> None = [];

    public static IReadOnlySet<string> For(string? role) => role switch
    {
        RoleNames.HRAdministrator => HrAdmin,
        RoleNames.Management => Management,
        _ => None,
    };

    public static bool Has(string? role, string permission) => For(role).Contains(permission);
}
