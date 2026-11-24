using System.Windows;
using Vinex.HR.Desktop.Core;

namespace Vinex.HR.Desktop.Views;

public partial class DialogWindow : Window
{
    public DialogWindow(IDialogViewModel viewModel, string title)
    {
        InitializeComponent();
        Title = title;
        Host.Content = viewModel;
        viewModel.CloseRequested += result =>
        {
            DialogResult = result;
            Close();
        };
    }
}
