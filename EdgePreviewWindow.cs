using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CodexTokenWidget;

public sealed class EdgePreviewWindow
{
    private const double Inset = 2;
    private readonly Popup popup;
    private readonly Border track;
    private readonly Border fill;
    private bool interactive;

    public EdgePreviewWindow()
    {
        var canvas = new Canvas { Margin = new Thickness(2), ClipToBounds = true };
        fill = new Border { Width = 2, Height = 0, CornerRadius = new CornerRadius(3) };
        canvas.Children.Add(fill);
        track = new Border { Width = 6, Height = 58, Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 255, 255, 255)), BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(190, 85, 90, 98)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Opacity = 0.82, Child = canvas };
        track.MouseEnter += (_, _) => NotifyPointerEntered();
        track.MouseMove += (_, _) => NotifyPointerEntered();
        popup = new Popup { Child = track, Placement = PlacementMode.AbsolutePoint, AllowsTransparency = true, StaysOpen = true };
    }

    public event Action? PointerEntered;

    private void NotifyPointerEntered() { if (interactive) PointerEntered?.Invoke(); }

    public void ShowAt(Rect workArea, Dock side, double centerY, int percent, double scale, bool isInteractive = false)
    {
        interactive = isInteractive;
        track.IsHitTestVisible = isInteractive;
        var width = Math.Max(6, Math.Round(8 * scale));
        var height = Math.Max(58, Math.Round(86 * scale));
        track.Width = width;
        track.Height = height;
        fill.Width = Math.Max(2, width - 4);
        fill.Height = percent <= 0 ? 0 : Math.Max(3, (height - 4) * Math.Clamp(percent, 0, 100) / 100d);
        fill.Background = UsageBrush(percent);
        Canvas.SetLeft(fill, 0);
        Canvas.SetBottom(fill, 0);
        popup.HorizontalOffset = side == Dock.Left ? workArea.Left + Inset : workArea.Right - width - Inset;
        popup.VerticalOffset = Math.Clamp(centerY - height / 2, workArea.Top + Inset, workArea.Bottom - height - Inset);
        popup.IsOpen = true;
    }

    public void UpdateProgress(int percent)
    {
        var normalized = Math.Clamp(percent, 0, 100);
        fill.Height = normalized == 0 ? 0 : Math.Max(3, (track.Height - 4) * normalized / 100d);
        fill.Background = UsageBrush(normalized);
    }

    public void Hide() { interactive = false; popup.IsOpen = false; }
    public void Close() => Hide();
    private static SolidColorBrush UsageBrush(int percent) => new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(percent > 80 ? "#EF5B5B" : percent > 50 ? "#F4B63F" : "#43C979"));
}



