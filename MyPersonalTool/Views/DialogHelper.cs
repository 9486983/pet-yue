using Avalonia;
using Avalonia.Controls;

namespace MyPersonalTool.Views;

internal static class DialogHelper
{
    public static void PositionAboveOwner(Window popup, Window owner, int aboveOffset = 300, int dialogWidth = 380)
    {
        const int margin = 10;
        var screen = owner.Screens.ScreenFromWindow(owner);
        if (screen == null) { popup.WindowStartupLocation = WindowStartupLocation.CenterScreen; return; }

        // screen.Bounds 和 Window.Position 都是 DIPs，同一坐标系无需转换
        var scrLeft = screen.Bounds.X;
        var scrRight = screen.Bounds.X + screen.Bounds.Width;
        var scrTop = screen.Bounds.Y;
        var scrBottom = screen.Bounds.Y + screen.Bounds.Height;

        // 水平：居中 → 右对齐宠物 → 贴屏幕（三级降级）
        var x = owner.Position.X + (int)((owner.Width - dialogWidth) / 2);
        if (x + dialogWidth + margin > scrRight)
        {
            x = owner.Position.X + (int)owner.Width - dialogWidth - margin;
            if (x + dialogWidth + margin > scrRight)
                x = scrRight - dialogWidth - margin;
        }
        if (x < scrLeft + margin)
            x = scrLeft + margin;

        // 纵向：上方优先 → 下方 → 贴屏幕
        var dlgH = 420;
        var y = owner.Position.Y - aboveOffset;
        if (y < scrTop + margin)
            y = owner.Position.Y + (int)owner.Height + margin;
        if (y + dlgH + margin > scrBottom)
            y = scrBottom - dlgH - margin;

        popup.Position = new PixelPoint(x, y);
    }
}
