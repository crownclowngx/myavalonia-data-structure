using Avalonia.Controls;
using DataStructurePlugin.Features.AhoCorasick;
using DataStructurePlugin.Plugin;
using Microsoft.Extensions.DependencyInjection;
using MyAvaloniaManagement.PluginSdk;

namespace DataStructurePlugin.Standalone;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 独立窗口复用插件的服务组合入口；仅负责建立一个页面作用域，不复制算法或时钟配置。
        var services = new ServiceCollection().AddDataStructurePluginServices()
            .AddScoped<AhoCorasickDocument>().BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        var scope = services.CreateScope();
        try
        {
            var document = scope.ServiceProvider.GetRequiredService<AhoCorasickDocument>();
            document.InitializeAsync(new NewDocumentActivation("AC 自动机 · 交互课堂"), CancellationToken.None)
                .GetAwaiter().GetResult();
            DataContext = document;
            Closed += (_, _) => { scope.Dispose(); services.Dispose(); };
        }
        catch
        {
            scope.Dispose();
            services.Dispose();
            throw;
        }
    }
}
