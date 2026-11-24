using System.Windows;
using Vinex.HR.Desktop.ViewModels;

namespace Vinex.HR.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
