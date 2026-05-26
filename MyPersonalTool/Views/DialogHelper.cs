using Avalonia;
using Avalonia.Controls;

namespace MyPersonalTool.Views;

internal static class DialogHelper
{
    /// <summary>将弹窗定位到宠物旁边，优先上方，空间不足时自动切换方向</summary>
    public static void PositionAboveOwner(Window popup, Window owner, int aboveOffset = 300, int dialogWidth = 380)
    {
        const int margin = 10;
        var screen = owner.Screens.ScreenFromWindow(owner);
        if (screen == null) { popup.WindowStartupLocation = WindowStartupLocation.CenterScreen; return; }

        var wa = screen.WorkingArea;
        var dlgH = Math.Min(400, wa.Height - margin * 2); // 预估弹窗高度

        // 水平：默认居中于宠物，右侧溢出则靠右，左侧溢出则靠左
        var x = owner.Position.X + (int)((owner.Width - dialogWidth) / 2);
        if (x + dialogWidth + margin > wa.X + wa.Width)
            x = wa.X + wa.Width - dialogWidth - margin;
        if (x < wa.X + margin)
            x = wa.X + margin;

        // 纵向：上方优先，空间不足则下方
        var y = owner.Position.Y - aboveOffset;
        if (y < wa.Y + margin)
            y = owner.Position.Y + (int)owner.Height + margin;

        // 底部溢出保护
        if (y + dlgH > wa.Y + wa.Height - margin)
            y = wa.Y + wa.Height - dlgH - margin;

        popup.Position = new PixelPoint(x, y);
    }
}
