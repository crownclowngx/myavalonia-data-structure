namespace DataStructurePlugin.Features.AhoCorasick.Application;

/// <summary>
/// 播放器只依赖“请求下一拍”的能力，不依赖 Avalonia 的 DispatcherTimer。
/// 生产适配器在 UI 线程发出节拍；测试适配器由测试显式推进，避免用固定等待推测时间。
/// 每个 Document 独占一个时钟，Stop 后播放器仍会防御性忽略已经排队的节拍。
/// </summary>
public interface IAcPlaybackClock : IDisposable
{
    event EventHandler? Tick;
    void Start(TimeSpan interval);
    void Stop();
}
