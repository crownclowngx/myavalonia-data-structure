using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using System.ComponentModel;

namespace DataStructurePlugin.Features.AhoCorasick.Controls;

/// <summary>阅读布局和换章滚动属于 View；章节选择与实验状态属于同一个 Document。</summary>
public partial class AcTutorialView : UserControl
{
    private AhoCorasickDocument? _document;
    private bool? _narrow;

    public AcTutorialView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => AdaptLayout();
    }

    private void AdaptLayout()
    {
        var narrow = Bounds.Width < 950;
        if (_narrow == narrow) return;
        _narrow = narrow;
        TutorialRail.IsVisible = !narrow;
        TutorialCompactChapters.IsVisible = narrow;
        TutorialGrid.ColumnDefinitions = new ColumnDefinitions(narrow ? "0,*" : "242,*");
        TutorialGrid.ColumnSpacing = narrow ? 0 : 18;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        Unsubscribe();
        base.OnDataContextChanged(e);
        TutorialScroll.Offset = default;
        Subscribe();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Subscribe();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Unsubscribe();
        base.OnDetachedFromVisualTree(e);
    }

    private void Subscribe()
    {
        if (_document is not null || !this.IsAttachedToVisualTree() || DataContext is not AhoCorasickDocument document) return;
        _document = document;
        document.PropertyChanged += OnDocumentChanged;
    }

    private void Unsubscribe()
    {
        if (_document is not null) _document.PropertyChanged -= OnDocumentChanged;
        _document = null;
    }

    private void OnDocumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AhoCorasickDocument.SelectedTutorialChapter)) TutorialScroll.Offset = default;
    }
}
