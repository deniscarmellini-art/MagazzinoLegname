using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MagazzinoLegname.Behaviors;

public static class NumericTextBoxBehavior
{
    public static readonly DependencyProperty SelectAllOnFocusProperty =
        DependencyProperty.RegisterAttached(
            "SelectAllOnFocus",
            typeof(bool),
            typeof(NumericTextBoxBehavior),
            new PropertyMetadata(false, OnSelectAllOnFocusChanged));

    public static bool GetSelectAllOnFocus(DependencyObject element) =>
        (bool)element.GetValue(SelectAllOnFocusProperty);

    public static void SetSelectAllOnFocus(DependencyObject element, bool value) =>
        element.SetValue(SelectAllOnFocusProperty, value);

    private static void OnSelectAllOnFocusChanged(DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not TextBox textBox) return;

        if ((bool)args.NewValue)
        {
            textBox.GotKeyboardFocus += SelectAll;
            textBox.PreviewMouseLeftButtonDown += FocusBeforeMouseSelection;
        }
        else
        {
            textBox.GotKeyboardFocus -= SelectAll;
            textBox.PreviewMouseLeftButtonDown -= FocusBeforeMouseSelection;
        }
    }

    private static void SelectAll(object sender, KeyboardFocusChangedEventArgs args) =>
        ((TextBox)sender).SelectAll();

    private static void FocusBeforeMouseSelection(object sender, MouseButtonEventArgs args)
    {
        var textBox = (TextBox)sender;
        if (textBox.IsKeyboardFocusWithin) return;

        args.Handled = true;
        textBox.Focus();
    }
}
