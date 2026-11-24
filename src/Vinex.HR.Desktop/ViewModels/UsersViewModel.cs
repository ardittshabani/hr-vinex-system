using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Vinex.HR.Contracts.Security;
using Vinex.HR.Contracts.Users;
using Vinex.HR.Desktop.Core;
using Vinex.HR.Desktop.Services;

namespace Vinex.HR.Desktop.ViewModels;

public sealed class UserRow(UserDto dto)
{
    public UserDto Dto { get; } = dto;
    public int Id => Dto.Id;
    public string Username => Dto.Username;
    public string DisplayName => Dto.DisplayName;
    public string Email => Dto.Email ?? string.Empty;
    public string RoleText => RoleNames.Display(Dto.Role);
    public bool IsActive => Dto.IsActive;
    public string StatusText => Dto.IsActive ? "Active" : "Inactive";
    public string LastLoginText => Dto.LastLoginAt is { } t ? t.LocalDateTime.ToString("dd.MM.yyyy HH:mm") : "Never";
}

public sealed partial class UsersViewModel : ViewModelBase
{
    private readonly UsersApi _api;
    private readonly IDialogService _dialogs;
    private readonly IServiceProvider _services;
    private readonly SessionState _session;

    public ObservableCollection<UserRow> Users { get; } = [];

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(ResetPasswordCommand), nameof(ToggleActiveCommand))]
    [NotifyPropertyChangedFor(nameof(ToggleActiveText))]
    private UserRow? _selectedUser;

    public string ToggleActiveText => SelectedUser?.IsActive == false ? "Activate" : "Deactivate";

    public UsersViewModel(UsersApi api, IDialogService dialogs, IServiceProvider services, SessionState session)
    {
        _api = api;
        _dialogs = dialogs;
        _services = services;
        _session = session;
        _ = LoadAsync();
    }

    private bool HasSelection() => SelectedUser is not null;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var page = await _api.ListAsync(Search);
            var selectedId = SelectedUser?.Id;
            Users.Clear();
            foreach (var u in page.Items)
            {
                Users.Add(new UserRow(u));
            }

            SelectedUser = Users.FirstOrDefault(u => u.Id == selectedId);
        });
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var editor = _services.GetRequiredService<UserEditorViewModel>();
        await editor.InitializeAsync(null);
        if (_dialogs.ShowDialog(editor, "Add user") == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedUser is null)
        {
            return;
        }

        var editor = _services.GetRequiredService<UserEditorViewModel>();
        await editor.InitializeAsync(SelectedUser.Dto);
        if (_dialogs.ShowDialog(editor, "Edit user") == true)
        {
            await LoadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ResetPassword()
    {
        if (SelectedUser is null)
        {
            return;
        }

        var vm = _services.GetRequiredService<ResetPasswordViewModel>();
        vm.Target = SelectedUser.Dto;
        _dialogs.ShowDialog(vm, "Reset password");
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ToggleActiveAsync()
    {
        var user = SelectedUser;
        if (user is null)
        {
            return;
        }

        if (user.Id == _session.User?.Id && user.IsActive)
        {
            ErrorMessage = "You cannot deactivate your own account.";
            return;
        }

        var verb = user.IsActive ? "deactivate" : "activate";
        if (!_dialogs.Confirm($"Do you want to {verb} {user.DisplayName}?", "Confirm"))
        {
            return;
        }

        await RunAsync(() => _api.SetActiveAsync(user.Id, !user.IsActive));
        if (!HasErrorMessage)
        {
            await LoadAsync();
        }
    }

    partial void OnSearchChanged(string value) => _ = LoadAsync();
}
