using DataStructurePlugin.Features.AhoCorasick;
using DataStructurePlugin.Features.AhoCorasick.Application;
using DataStructurePlugin.Features.AhoCorasick.Domain;

namespace DataStructurePlugin.Tests;

/// <summary>
/// 测试时钟不会自行等待。测试可以精确推进一拍，也能模拟 Stop 后仍到达的一拍，验证迟到回调防护。
/// 它与生产时钟遵守同一小接口，避免把 UI 计时的不确定性引入领域与 Document 单元测试。
/// </summary>
internal sealed class TestPlaybackClock : IAcPlaybackClock
{
    public event EventHandler? Tick;
    public bool Running { get; private set; }
    public TimeSpan Interval { get; private set; }
    public bool Disposed { get; private set; }
    public int StartCount { get; private set; }
    public void Start(TimeSpan interval) { Interval = interval; Running = true; StartCount++; }
    public void Stop() => Running = false;
    public void Fire() => Tick?.Invoke(this, EventArgs.Empty);
    public Action CapturePendingTick()
    {
        var pending = Tick;
        return () => pending?.Invoke(this, EventArgs.Empty);
    }
    public void Dispose() { Running = false; Disposed = true; Tick = null; }
}

internal static class TestDocuments
{
    public static AhoCorasickDocument Create() => new(new AhoCorasickTraceBuilder(), new TestPlaybackClock());
}
