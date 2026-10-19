using Avalonia.Controls;
using DataStructurePlugin.Constants;
using DataStructurePlugin.Features.AhoCorasick;
using DataStructurePlugin.Features.AhoCorasick.Application;
using DataStructurePlugin.Features.AhoCorasick.Domain;
using DataStructurePlugin.Plugin;
using Microsoft.Extensions.DependencyInjection;
using MyAvaloniaManagement.PluginSdk;
using MyAvaloniaManagement.PluginSdk.UI;
using Xunit;

namespace DataStructurePlugin.Tests;

public sealed class AcCompositionTests
{
    [Fact]
    public void 真实Module只注册AC域且多个Document使用独立时钟()
    {
        var registration = new RecordingRegistration();
        new DataStructurePluginModule().Configure(registration);
        var entry = Assert.Single(registration.Documents);
        Assert.Equal(AhoCorasickIds.Document, entry.Descriptor.DocumentTypeId);
        Assert.Equal(typeof(AhoCorasickDocument), entry.Model);
        Assert.Equal(typeof(AhoCorasickView), entry.View);
        Assert.Equal(registration.IconReference, entry.Descriptor.IconPath);
        Assert.Equal(0, registration.OtherContributions);
        Assert.Equal(ServiceLifetime.Singleton, registration.Services.Single(s => s.ServiceType == typeof(IAcTraceBuilder)).Lifetime);
        Assert.Equal(ServiceLifetime.Transient, registration.Services.Single(s => s.ServiceType == typeof(IAcPlaybackClock)).Lifetime);
        using var services = registration.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var firstScope = services.CreateScope();
        using var secondScope = services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<AhoCorasickDocument>();
        var second = secondScope.ServiceProvider.GetRequiredService<AhoCorasickDocument>();
        Assert.Same(first, firstScope.ServiceProvider.GetRequiredService<AhoCorasickDocument>());
        Assert.NotSame(first, second);
        Assert.NotSame(firstScope.ServiceProvider.GetRequiredService<IAcPlaybackClock>(), secondScope.ServiceProvider.GetRequiredService<IAcPlaybackClock>());
        Assert.Same(firstScope.ServiceProvider.GetRequiredService<IAcTraceBuilder>(), secondScope.ServiceProvider.GetRequiredService<IAcTraceBuilder>());
        first.FinishCommand.Execute(null);
        Assert.Equal(0, second.FrameIndex);
    }

    /// <summary>只记录 SDK 注册事实，不启动 Host；未预期的全局贡献会计数并让断言失败。</summary>
    private sealed class RecordingRegistration : IPluginRegistration, IPluginIconRegistration, IWorkbenchCommandRegistration
    {
        public PluginId PluginId => PluginIds.Plugin;
        public IServiceCollection Services { get; } = new ServiceCollection();
        public List<(DocumentDescriptor Descriptor, Type Model, Type View)> Documents { get; } = [];
        public int OtherContributions { get; private set; }
        public string IconReference { get; private set; } = "";
        public string AddIcon(string localName, VectorIconDefinition definition) => IconReference = $"capture:{localName}";
        public void AddDocument<TDocument, TView>(DocumentDescriptor descriptor)
            where TDocument : class, IPluginDocument where TView : Control, new()
        {
            Documents.Add((descriptor, typeof(TDocument), typeof(TView)));
            Services.AddScoped<TDocument>();
        }
        public void AddPersistableDocument<TDocument, TView>(DocumentDescriptor descriptor)
            where TDocument : class, IPersistablePluginDocument where TView : Control, new() => OtherContributions++;
        public void AddTool<TTool, TView>(ToolDescriptor descriptor)
            where TTool : class where TView : Control, new() => OtherContributions++;
        public void UseLifecycle<TLifecycle>() where TLifecycle : class, IPluginLifecycle => OtherContributions++;
        public void AddDocumentCommand(CommandDescriptor descriptor, DocumentTypeId targetDocumentTypeId) => OtherContributions++;
        public void AddMenuCommandContribution(MenuCommandContributionDescriptor descriptor) => OtherContributions++;
        public void AddKeyBindingContribution(KeyBindingContributionDescriptor descriptor) => OtherContributions++;
    }
}
