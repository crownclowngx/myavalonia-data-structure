using Avalonia.Media;

namespace DataStructurePlugin.Features.AhoCorasick.Controls;

/// <summary>
/// AC 自绘控件共用的不可变画笔集合，仅属于本域的呈现层。
/// 设计思路：由 View 的主题字典通过 DynamicResource 选择整套配色，控件只读取画笔。
/// 主题变化只使绘图失效，不重建轨迹、布局或 Document，也不由控件猜测 Host 的主题。
/// 当前节点使用深青底与白字；深色主题的普通强调线另用亮青色，保证两种用途各自有足够对比。
/// </summary>
public sealed record AcDrawingPalette(
    IImmutableSolidColorBrush Text, IImmutableSolidColorBrush Muted,
    IImmutableSolidColorBrush Current, IImmutableSolidColorBrush OnCurrent,
    IImmutableSolidColorBrush Accent, IImmutableSolidColorBrush Match,
    IImmutableSolidColorBrush Canvas, IImmutableSolidColorBrush Dot,
    IImmutableSolidColorBrush Edge, IImmutableSolidColorBrush Failure,
    IImmutableSolidColorBrush Node, IImmutableSolidColorBrush NodeBorder,
    IImmutableSolidColorBrush Selection, IImmutableSolidColorBrush Candidate,
    IImmutableSolidColorBrush Consumed, IImmutableSolidColorBrush Unread)
{
    public static AcDrawingPalette Light { get; } = new(
        Ink("#243D3D"), Ink("#53665D"), Ink("#087F78"), Ink("#FFFFFF"),
        Ink("#087F78"), Ink("#A74E29"), Ink("#F5F8F6"), Ink("#DCE5E0"),
        Ink("#718B80"), Ink("#A66B4D"), Ink("#FFFFFF"), Ink("#718B80"),
        Ink("#529B8E"), Ink("#FFF0DF"), Ink("#E3EFE4"), Ink("#F2F4EE"));

    public static AcDrawingPalette Dark { get; } = new(
        Ink("#E3ECE7"), Ink("#ABBBB2"), Ink("#087F78"), Ink("#FFFFFF"),
        Ink("#65D7C8"), Ink("#F0AE88"), Ink("#1B2425"), Ink("#344447"),
        Ink("#819F95"), Ink("#C99376"), Ink("#263537"), Ink("#819F95"),
        Ink("#80C5B6"), Ink("#55402B"), Ink("#2B443D"), Ink("#293437"));

    private static IImmutableSolidColorBrush Ink(string color) => new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse(color));
}
