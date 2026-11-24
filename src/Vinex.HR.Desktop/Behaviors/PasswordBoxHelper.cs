using System.Windows;
using System.Windows.Controls;

namespace Vinex.HR.Desktop.Behaviors;

/// <summary>Attached property that makes PasswordBox.Password bindable (two-way).</summary>
public static class PasswordBoxHelper
{
    public static readonly DependencyProperty BoundPasswordProperty = DependencyProperty.RegisterAttached(
        "BoundPassword",
        typeof(string),
        typeof(PasswordBoxHelper),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    private static readonly DependencyProperty UpdatingProperty =
        DependencyProperty.RegisterAttached("Updating", typeof(bool), typeof(PasswordBoxHelper), new PropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject d) => (string)d.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject d, string value) => d.SetValue(BoundPasswordProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box)
        {
            return;
        }

        box.PasswordChanged -= OnPasswordChanged;
        if (!(bool)box.GetValue(UpdatingProperty))
        {
            var newValue = e.NewValue as string ?? string.Empty;
            if (box.Password != newValue)
            {
                box.Password = newValue;
            }
        }

        box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        box.SetValue(UpdatingProperty, true);
        SetBoundPassword(box, box.Password);
        box.SetValue(UpdatingProperty, false);
    }
}
