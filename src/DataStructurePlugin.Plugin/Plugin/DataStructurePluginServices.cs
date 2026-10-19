using Microsoft.Extensions.DependencyInjection;
using DataStructurePlugin.Features.AhoCorasick.Application;
using DataStructurePlugin.Features.AhoCorasick.Domain;
using DataStructurePlugin.Features.AhoCorasick.Infrastructure;

namespace DataStructurePlugin.Plugin;

public static class DataStructurePluginServices
{
    /// <summary>登记插件自己的业务服务；Standalone 可以复用同一个组合入口。</summary>
    public static IServiceCollection AddDataStructurePluginServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 构建器无状态，可共享；播放时钟有生命周期，每次解析都必须独立，不能成为跨 Document 单例。
        services.AddSingleton<IAcTraceBuilder, AhoCorasickTraceBuilder>();
        services.AddTransient<IAcPlaybackClock, DispatcherAcPlaybackClock>();
        return services;
    }
}
