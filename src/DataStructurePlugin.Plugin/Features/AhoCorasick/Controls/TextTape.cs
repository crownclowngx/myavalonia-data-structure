using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DataStructurePlugin.Features.AhoCorasick.Domain;

namespace DataStructurePlugin.Features.AhoCorasick.Controls;

/// <summary>
/// 当前字符附近的文本窗口。字符格按码点展示，格下数字保留原文 UTF-16 偏移；最近一次匹配用下划线标记。
/// 控件只投影当前步骤，不自行前进游标或搜索关键词，防止画面和真实匹配轨迹分离。
/// </summary>
public sealed class TextTape : Control
{
    public static readonly StyledProperty<AcDrawingPalette> PaletteProperty = AvaloniaProperty.Register<TextTape, AcDrawingPalette>(nameof(Palette), AcDrawingPalette.Light);
    public AcDrawingPalette Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }
    public static readonly StyledProperty<AcTrace?> TraceProperty = AvaloniaProperty.Register<TextTape, AcTrace?>(nameof(Trace));
    public static readonly StyledProperty<AcStep?> StepProperty = AvaloniaProperty.Register<TextTape, AcStep?>(nameof(Step));
    public AcTrace? Trace { get => GetValue(TraceProperty); set => SetValue(TraceProperty, value); }
    public AcStep? Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    // 和轨迹、步骤一样，主题也是渲染输入；切换主题不能要求用户再按一步才能看到新颜色。
    static TextTape() => AffectsRender<TextTape>(TraceProperty, StepProperty, PaletteProperty);
    public TextTape() => ClipToBounds = true;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Trace is not { } trace || Step is not { } step) return;
        if (trace.Symbols.Length == 0) { Text(context, "ε  空文本，没有字符需要扫描。", new Point(0, 12), Palette.Muted, 12); return; }
        var capacity = Math.Max(1, (int)(Bounds.Width / 44));
        var focus = step.ActiveSymbol >= 0 ? step.ActiveSymbol : Math.Max(0, step.ConsumedSymbols - 1);
        var start = Math.Clamp(focus - capacity / 2, 0, Math.Max(0, trace.Symbols.Length - capacity));
        var recent = step.MatchCount > 0 ? trace.Matches[step.MatchCount - 1] : null;
        for (var i = start; i < Math.Min(trace.Symbols.Length, start + capacity); i++)
        {
            var symbol = trace.Symbols[i];
            var x = (i - start) * 44d;
            var active = step.ActiveSymbol == i;
            var consumed = i < step.ConsumedSymbols;
            var rect = new Rect(x, 0, 38, 36);
            context.DrawRectangle(active ? Palette.Current : consumed ? Palette.Consumed : Palette.Unread, null, rect, 6, 6);
            var label = symbol.Text switch { " " => "␣", "\n" => "↵", "\r" => "↵", "\t" => "⇥", _ => symbol.Text };
            Text(context, label, new Point(x + 10, 7), active ? Palette.OnCurrent : Palette.Text, 15);
            Text(context, symbol.Offset.ToString(CultureInfo.InvariantCulture), new Point(x + 12, 44), Palette.Muted, 10);
            if (recent is not null && symbol.Offset >= recent.Start && symbol.Offset < recent.End)
                context.DrawLine(new Pen(Palette.Match, 3), new Point(x + 3, 40), new Point(x + 35, 40));
        }
    }
    private static void Text(DrawingContext context, string text, Point position, IBrush brush, double size) =>
        context.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI, Segoe UI"), size, brush), position);
}
