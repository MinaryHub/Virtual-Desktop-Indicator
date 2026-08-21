using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DeskCue.Linux.Services;

/// <summary>Tiny code-built message/confirm dialog (Avalonia ships no MessageBox).</summary>
public static class Dialogs
{
    private const string Caption = "DeskCue";

    public static Task ShowInfoAsync(string message) => ShowAsync(message, confirm: false);

    /// <summary>Returns true if the user confirmed (Yes).</summary>
    public static Task<bool> ConfirmAsync(string message) => ShowAsync(message, confirm: true);

    private static Task<bool> ShowAsync(string message, bool confirm)
    {
        var tcs = new TaskCompletionSource<bool>();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        var win = new Window
        {
            Title = Caption,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            ShowInTaskbar = false,
        };

        void Finish(bool result) { tcs.TrySetResult(result); win.Close(); }

        if (confirm)
        {
            var yes = new Button { Content = "Yes", MinWidth = 84, IsDefault = true };
            yes.Click += (_, _) => Finish(true);
            var no = new Button { Content = "No", MinWidth = 84, IsCancel = true };
            no.Click += (_, _) => Finish(false);
            buttons.Children.Add(yes);
            buttons.Children.Add(no);
        }
        else
        {
            var ok = new Button { Content = "OK", MinWidth = 84, IsDefault = true };
            ok.Click += (_, _) => Finish(true);
            buttons.Children.Add(ok);
        }

        win.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(18),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 13 },
                buttons,
            },
        };

        // If dismissed via the window manager, resolve to a safe default.
        win.Closed += (_, _) => tcs.TrySetResult(!confirm);
        win.Show();
        return tcs.Task;
    }
}
