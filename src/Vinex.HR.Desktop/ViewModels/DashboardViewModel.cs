using Vinex.HR.Desktop.Core;

namespace Vinex.HR.Desktop.ViewModels;

public sealed class DashboardViewModel(SessionState session)
{
    public string Greeting => $"Welcome, {session.User?.DisplayName}";
    public string Subtitle => "Live statistics (headcount, attendance, leave, holidays) arrive with Phases 2-5.";
}

public sealed class ComingSoonViewModel(string title, string phase, string description)
{
    public string Title { get; } = title;
    public string Phase { get; } = phase;
    public string Description { get; } = description;
}
