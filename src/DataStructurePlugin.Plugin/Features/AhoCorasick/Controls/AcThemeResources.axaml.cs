using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace DataStructurePlugin.Features.AhoCorasick.Controls;

/// <summary>本域的主题资源。只在创建资源字典时接入不可变画笔，不监听或修改全局主题。</summary>
public partial class AcThemeResources : ResourceDictionary
{
    public AcThemeResources()
    {
        AvaloniaXamlLoader.Load(this);
        // 以与普通画刷相同的主题键提供自绘配色，资源继承与切换统一交给 Avalonia 处理。
        ((ResourceDictionary)ThemeDictionaries[ThemeVariant.Light])["Ac.DrawingPalette"] = AcDrawingPalette.Light;
        ((ResourceDictionary)ThemeDictionaries[ThemeVariant.Dark])["Ac.DrawingPalette"] = AcDrawingPalette.Dark;
    }
}
