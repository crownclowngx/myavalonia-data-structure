using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DataStructurePlugin.Features.AhoCorasick.Domain;

namespace DataStructurePlugin.Features.AhoCorasick.Controls;

/// <summary>
/// AC 图的纯呈现控件：只读取当前不可变轨迹和步骤，负责布局、绘制以及指针命中。
/// 状态转移和 fail 构建全部由领域层决定；用户点击只报告节点身份，由 Document 更新选择。
/// 布局基于完整 Trie 固定下来，回放插入过程只改变可见范围，避免节点每一步重新排队。
/// </summary>
public sealed class AutomatonCanvas : Control
{
    public static readonly StyledProperty<AcDrawingPalette> PaletteProperty = AvaloniaProperty.Register<AutomatonCanvas, AcDrawingPalette>(nameof(Palette), AcDrawingPalette.Light);
    public AcDrawingPalette Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }
    public static readonly StyledProperty<AcTrace?> TraceProperty = AvaloniaProperty.Register<AutomatonCanvas, AcTrace?>(nameof(Trace));
    public static readonly StyledProperty<AcStep?> StepProperty = AvaloniaProperty.Register<AutomatonCanvas, AcStep?>(nameof(Step));
    public static readonly StyledProperty<bool> ShowFailuresProperty = AvaloniaProperty.Register<AutomatonCanvas, bool>(nameof(ShowFailures), true);
    public static readonly StyledProperty<int> SelectedNodeProperty = AvaloniaProperty.Register<AutomatonCanvas, int>(nameof(SelectedNode));
    public AcTrace? Trace { get => GetValue(TraceProperty); set => SetValue(TraceProperty, value); }
    public AcStep? Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public bool ShowFailures { get => GetValue(ShowFailuresProperty); set => SetValue(ShowFailuresProperty, value); }
    public int SelectedNode { get => GetValue(SelectedNodeProperty); set => SetValue(SelectedNodeProperty, value); }
    public event Action<int>? NodeSelected;
    private AcTrace? _layoutTrace;
    private Point[] _layout = [];
    private Point[] _screen = [];
    private double _zoom = 1;
    private Vector _pan;
    private Point? _drag;
    private bool _moved;
    private double _nodeRadius = 23;
    // 调色板是绘图输入之一。动态资源替换它时只重绘，保留缩放、平移和选中节点。
    static AutomatonCanvas() => AffectsRender<AutomatonCanvas>(TraceProperty, StepProperty, ShowFailuresProperty, SelectedNodeProperty, PaletteProperty);

    public AutomatonCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        AutomationProperties.SetName(this, "AC 自动机。点击节点检查；拖动平移，滚轮缩放，按 0 复位。");
    }

    public void ResetViewport() { _zoom = 1; _pan = default; InvalidateVisual(); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(Palette.Canvas, null, new Rect(Bounds.Size));
        for (var x = 18d; x < Bounds.Width; x += 24)
            for (var y = 18d; y < Bounds.Height; y += 24)
                context.DrawEllipse(Palette.Dot, null, new Point(x, y), 0.8, 0.8);
        if (Trace is not { } trace || Step is not { } step || Bounds.Width < 1) return;
        EnsureLayout(trace);
        var width = Math.Max(110, _layout.Max(p => p.X));
        var height = Math.Max(88, _layout.Max(p => p.Y));
        var scaleX = Math.Max(0.04, Math.Min(2.1, (Bounds.Width - 100) / width) * _zoom);
        var scaleY = Math.Max(0.04, Math.Min(1.1, (Bounds.Height - 90) / height) * _zoom);
        var scale = Math.Min(1.2, Math.Min(scaleX, scaleY) * 1.4);
        var origin = new Point((Bounds.Width - _layout.Max(p => p.X) * scaleX) / 2, 38) + _pan;
        _screen = _layout.Select(p => origin + new Vector(p.X * scaleX, p.Y * scaleY)).ToArray();
        var radius = Math.Clamp(23 * scale, 8, 25);
        _nodeRadius = radius;

        for (var i = 0; i < step.VisibleNodes; i++)
        {
            foreach (var (character, target) in trace.Nodes[i].Edges)
            {
                if (target >= step.VisibleNodes) continue;
                var highlighted = step.EdgeFrom == i && step.EdgeTo == target && step.Action is AcAction.Insert or AcAction.Reuse or AcAction.Compare or AcAction.Transition;
                var color = highlighted ? Palette.Accent : Palette.Edge;
                DrawArrow(context, _screen[i], _screen[target], color, highlighted ? 3 : 1.5, radius);
                var mid = new Point((_screen[i].X + _screen[target].X) / 2 + 10, (_screen[i].Y + _screen[target].Y) / 2 - 7);
                if (scale > 0.35) DrawText(context, new System.Text.Rune(character).ToString(), mid, color, 12);
            }
        }
        for (var i = 1; i < step.VisibleNodes; i++)
        {
            var target = step.Failures[i];
            var highlighted = step.EdgeFrom == i && step.EdgeTo == target && step.Action is AcAction.SetFailure or AcAction.Fallback or AcAction.Candidate;
            if (target < 0 || (!ShowFailures && !highlighted)) continue;
            // 根节点的入链很多，只展示选中节点或当前事件相关的链，避免虚线遮盖主路径。
            if (target == 0 && i != SelectedNode && !highlighted) continue;
            var start = _screen[i];
            var end = _screen[target];
            var bend = Math.Min(95, Math.Abs(start.Y - end.Y) / 2 + 30);
            var cp1 = start + new Vector(bend, 3);
            var cp2 = end + new Vector(bend, -8);
            var delta = Delta(cp2, end);
            var length = Math.Max(1, delta.Length);
            end += delta / length * radius;
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                path.BeginFigure(start + new Vector(radius, 0), false);
                path.CubicBezierTo(cp1, cp2, end);
                path.EndFigure(false);
            }
            var brush = highlighted ? Palette.Match : Palette.Failure;
            context.DrawGeometry(null, new Pen(brush, highlighted ? 2.5 : 1.2, DashStyle.Dash), geometry);
            ArrowHead(context, cp2, end, brush);
        }
        for (var i = 0; i < step.VisibleNodes; i++)
        {
            var node = trace.Nodes[i];
            var terminal = node.PatternId >= 0 && node.PatternId < step.CompletedPatterns;
            var current = i == step.State;
            var selected = i == SelectedNode;
            var candidate = i == step.Candidate;
            var fill = current ? Palette.Current : candidate ? Palette.Candidate : Palette.Node;
            var border = current ? Palette.Current : candidate ? Palette.Match : Palette.NodeBorder;
            if (selected) context.DrawEllipse(null, new Pen(Palette.Selection, 3), _screen[i], radius + 5, radius + 5);
            context.DrawEllipse(fill, new Pen(border, current ? 2.5 : 1.4), _screen[i], radius, radius);
            if (terminal) context.DrawEllipse(null, new Pen(current ? Palette.OnCurrent : Palette.Accent, 1), _screen[i], radius - 4, radius - 4);
            if (scale > 0.3)
            {
                var label = i == 0 ? "ε" : node.Prefix;
                if (label.Length > 6) label = "…" + string.Concat(label.EnumerateRunes().TakeLast(3).Select(r => r.ToString()));
                DrawText(context, label, _screen[i], current ? Palette.OnCurrent : Palette.Text, Math.Clamp(12 * scale, 8, 12), true);
            }
        }
        DrawText(context, "滚轮缩放 · 拖动平移 · 点击检查节点", new Point(14, Bounds.Height - 24), Palette.Muted, 10);
    }

    private void EnsureLayout(AcTrace trace)
    {
        if (ReferenceEquals(trace, _layoutTrace)) return;
        _layoutTrace = trace;
        _layout = new Point[trace.Nodes.Length];
        _zoom = 1;
        _pan = default;
        var leaf = 0;
        double Visit(int id)
        {
            // 不依赖不可变字典的枚举顺序，按创建编号布局，相同输入得到稳定的节点位置。
            var children = trace.Nodes[id].Edges.Values.Order().ToArray();
            var x = children.Length == 0 ? leaf++ * 110d : children.Select(Visit).Average();
            _layout[id] = new(x, trace.Nodes[id].Depth * 88d);
            return x;
        }
        Visit(0);
    }

    private static void DrawText(DrawingContext context, string text, Point point, IBrush brush, double size, bool centered = false)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI, Segoe UI"), size, brush);
        context.DrawText(formatted, centered ? point - new Vector(formatted.Width / 2, formatted.Height / 2) : point);
    }

    private static void DrawArrow(DrawingContext context, Point from, Point to, IBrush brush, double thickness, double radius)
    {
        var delta = Delta(to, from);
        if (delta.Length < 1) return;
        var unit = delta / delta.Length;
        var start = from + unit * radius;
        var end = to - unit * radius;
        context.DrawLine(new Pen(brush, thickness), start, end);
        ArrowHead(context, start, end, brush);
    }
    private static void ArrowHead(DrawingContext context, Point from, Point end, IBrush brush)
    {
        var delta = Delta(end, from);
        if (delta.Length < 1) return;
        var unit = delta / delta.Length;
        var side = new Vector(-unit.Y, unit.X);
        context.DrawLine(new Pen(brush, 1.6), end, end - unit * 7 + side * 3);
        context.DrawLine(new Pen(brush, 1.6), end, end - unit * 7 - side * 3);
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Delta.Y == 0) return;
        _zoom = Math.Clamp(_zoom * (e.Delta.Y > 0 ? 1.15 : 1 / 1.15), 0.3, 8);
        InvalidateVisual();
        e.Handled = true;
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus();
        _drag = e.GetPosition(this);
        _moved = false;
        e.Pointer.Capture(this);
        e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag is not { } start) return;
        var point = e.GetPosition(this);
        var delta = Delta(point, start);
        if (delta.Length > 2 || _moved)
        {
            _pan += delta;
            _drag = point;
            _moved = true;
            InvalidateVisual();
        }
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is null) return;
        if (!_moved && Step is { } step)
        {
            var point = e.GetPosition(this);
            var nearest = -1;
            var distance = _nodeRadius + 5;
            for (var i = 0; i < Math.Min(step.VisibleNodes, _screen.Length); i++)
            {
                var candidate = Delta(_screen[i], point).Length;
                if (candidate <= distance) { nearest = i; distance = candidate; }
            }
            if (nearest >= 0) NodeSelected?.Invoke(nearest);
        }
        _drag = null;
        e.Pointer.Capture(null);
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        // 窗口失活或其他控件夺取捕获时，也要结束拖动，避免下一次移动继续平移旧画布。
        _drag = null;
        _moved = false;
        base.OnPointerCaptureLost(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.D0 or Key.NumPad0) { ResetViewport(); e.Handled = true; }
    }
    private static Vector Delta(Point left, Point right) => new(left.X - right.X, left.Y - right.Y);
}
