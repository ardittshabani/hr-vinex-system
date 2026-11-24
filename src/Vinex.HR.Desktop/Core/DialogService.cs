using System.Windows;
using Vinex.HR.Desktop.Views;

namespace Vinex.HR.Desktop.Core;

/// <summary>A view model shown inside the generic dialog window.</summary>
public interface IDialogViewModel
{
    event Action<bool?>? CloseRequested;
}

public interface IDialogService
{
    bool? ShowDialog(IDialogViewModel viewModel, string title);
    bool Confirm(string message, string title);
}

public sealed class DialogService : IDialogService
{
    public bool? ShowDialog(IDialogViewModel viewModel, string title)
    {
        var window = new DialogWindow(viewModel, title) { Owner = Application.Current.MainWindow };
        return window.ShowDialog();
    }

    public bool Confirm(string message, string title) =>
        MessageBox.Show(Application.Current.MainWindow, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
        == MessageBoxResult.Yes;
}
