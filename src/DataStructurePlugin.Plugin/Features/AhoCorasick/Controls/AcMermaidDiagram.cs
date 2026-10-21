using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace DataStructurePlugin.Features.AhoCorasick.Controls;

/// <summary>
/// 显示由同一 Markdown 源码生成的 Mermaid 图。资源按源码哈希定位，主题切换选择对应图，
/// 用户无需安装 Node、浏览器控件或访问在线渲染服务。源码保留在折叠区，可复制到 Mermaid 编辑器。
/// </summary>
public sealed class AcMermaidDiagram : Border
{
    private readonly Image _image = new() { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly ScrollViewer _viewport = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    private readonly string _hash;
    private Bitmap? _bitmap;
    private double _scale = 1;

    public AcMermaidDiagram(string source)
    {
        _hash = SourceHash(source);
        Classes.Add("md-code");
        var panel = new StackPanel { Spacing = 10 };
        var toolbar = new WrapPanel();
        toolbar.Children.Add(new TextBlock { Text = "Mermaid 图示", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
        var smaller = new Button { Content = "−", Padding = new Thickness(10, 3) };
        var larger = new Button { Content = "+", Padding = new Thickness(10, 3), Margin = new Thickness(5, 0, 5, 0) };
        var reset = new Button { Content = "适应宽度", Padding = new Thickness(10, 3) };
        AutomationProperties.SetName(smaller, "缩小教程图示");
        AutomationProperties.SetName(larger, "放大教程图示");
        smaller.Click += (_, _) => Zoom(_scale / 1.25);
        larger.Click += (_, _) => Zoom(_scale * 1.25);
        reset.Click += (_, _) => Zoom(1);
        toolbar.Children.Add(smaller);
        toolbar.Children.Add(larger);
        toolbar.Children.Add(reset);
        panel.Children.Add(toolbar);
        AutomationProperties.SetName(_image, "由本章 Mermaid 源码生成的流程图");
        _viewport.Content = _image;
        _viewport.SizeChanged += (_, _) => Zoom(_scale);
        panel.Children.Add(_viewport);
        panel.Children.Add(new Expander
        {
            Header = "查看 Mermaid 源码",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new SelectableTextBlock { Text = source, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Cascadia Code, Consolas, Microsoft YaHei UI"), FontSize = 12, Margin = new Thickness(0, 10, 0, 0) }
        });
        Child = panel;
        ActualThemeVariantChanged += (_, _) => { if (this.IsAttachedToVisualTree()) LoadImage(); };
    }

    public static string SourceHash(string source) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(source.Replace("\r\n", "\n", StringComparison.Ordinal).Trim())))[..16];

    private void Zoom(double scale)
    {
        _scale = Math.Clamp(scale, 0.25, 3);
        if (_bitmap is not null) _image.Width = Math.Min(_bitmap.PixelSize.Width / 2d, Math.Max(240, _viewport.Bounds.Width)) * _scale;
    }

    private void LoadImage()
    {
        var theme = ActualThemeVariant == ThemeVariant.Dark ? "dark" : "light";
        using var stream = typeof(AcMermaidDiagram).Assembly.GetManifestResourceStream($"DataStructurePlugin.AhoCorasick.Diagrams.{_hash}.{theme}.png")
            ?? throw new InvalidOperationException("Mermaid 图资源与教程不同步，请运行 tools/tutorial-assets 中的生成脚本。");
        var next = new Bitmap(stream);
        _image.Source = next;
        _bitmap?.Dispose();
        _bitmap = next;
        Zoom(_scale);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadImage();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _image.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }
}
