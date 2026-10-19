using MyAvaloniaManagement.PluginSdk.UI;
using MyAvaloniaManagement.Icons;
using DataStructurePlugin.Features.AhoCorasick;

namespace DataStructurePlugin.Plugin;

public sealed class DataStructurePluginModule : IPluginModule
{
    public void Configure(IPluginRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        registration.Services.AddDataStructurePluginServices();
        // 将插件自选版本的公共资源复制为 SDK 数据；Host 无需升级资源包即可显示这个别名。
        var asset = CommonIcons.TextCheck;
        var icon = registration.AddIcon("aho-corasick-document", new VectorIconDefinition(
            asset.PathData, asset.ViewBoxWidth, asset.ViewBoxHeight));
        registration.AddDocument<AhoCorasickDocument, AhoCorasickView>(
            new DocumentDescriptor(
                AhoCorasickIds.Document,
                "AC 自动机 · 交互课堂",
                "图解、数学与可回放实验：构建 Trie、理解 fail、扫描自己的句子。",
                "数据结构与算法", iconPath: icon));
    }
}
