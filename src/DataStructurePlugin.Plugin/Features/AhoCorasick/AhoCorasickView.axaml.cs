using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace DataStructurePlugin.Features.AhoCorasick;

/// <summary>只负责响应式布局与用户输入转发；窗口、算法和播放状态都不保存在 View 中。</summary>
public partial class AhoCorasickView : UserControl
{
    private AhoCorasickDocument? _boundDocument;
    private bool? _narrow;
    private bool? _stacked;
    private bool? _headerStacked;
    public AhoCorasickView()
    {
        InitializeComponent();
        Graph.NodeSelected += id => { if (DataContext is AhoCorasickDocument document) document.SelectedNodeId = id; };
        SizeChanged += (_, _) => AdaptLayout();
    }

    private void AdaptLayout()
    {
        var headerStacked = Bounds.Width < 950;
        if (_headerStacked != headerStacked)
        {
            _headerStacked = headerStacked;
            HeaderGrid.ColumnDefinitions = new ColumnDefinitions(headerStacked ? "*" : "*,Auto");
            HeaderGrid.RowDefinitions = new RowDefinitions(headerStacked ? "Auto,Auto" : "Auto");
            Grid.SetColumn(ModeToolbar, headerStacked ? 0 : 1);
            Grid.SetRow(ModeToolbar, headerStacked ? 1 : 0);
            ModeToolbar.Margin = headerStacked ? new Thickness(0, 12, 0, 0) : default;
        }
        var narrow = Bounds.Width < 1_100;
        if (_narrow != narrow)
        {
            _narrow = narrow;
            ChapterRail.IsVisible = !narrow;
            CompactChapters.IsVisible = narrow;
            BodyGrid.ColumnDefinitions = new ColumnDefinitions(narrow ? "0,*" : "188,*");
            BodyGrid.ColumnSpacing = narrow ? 0 : 18;
        }
        var stacked = Bounds.Width < 850;
        if (_stacked != stacked)
        {
            _stacked = stacked;
            WorkspaceGrid.ColumnDefinitions = new ColumnDefinitions(stacked ? "*" : "*,290");
            Grid.SetColumn(InspectorPanel, stacked ? 0 : 1);
            Grid.SetRow(InspectorPanel, stacked ? 1 : 0);
        }
    }

    private void ResetViewport(object? sender, RoutedEventArgs e) => Graph.ResetViewport();

    protected override void OnDataContextChanged(EventArgs e)
    {
        // Host 可以复用 View；替换模型同样意味着旧页面不再可见，必须先停止旧实例的播放。
        _boundDocument?.Pause();
        base.OnDataContextChanged(e);
        _boundDocument = DataContext as AhoCorasickDocument;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is AhoCorasickDocument document) document.Pause();
        base.OnDetachedFromVisualTree(e);
    }
}
