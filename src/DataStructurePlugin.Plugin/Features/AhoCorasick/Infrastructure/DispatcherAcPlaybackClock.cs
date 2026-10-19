using Avalonia.Threading;
using DataStructurePlugin.Features.AhoCorasick.Application;

namespace DataStructurePlugin.Features.AhoCorasick.Infrastructure;

/// <summary>
/// 将 Avalonia UI 节拍适配到小型时钟接口。这里仅管理定时器资源，不决定步骤、速度上限或终点行为。
/// 延迟创建定时器，使 DI 注册、容器验证及纯领域测试不必提前初始化 UI 平台。
/// </summary>
public sealed class DispatcherAcPlaybackClock : IAcPlaybackClock
{
    private DispatcherTimer? _timer;
    private bool _disposed;
    public event EventHandler? Tick;

    public void Start(TimeSpan interval)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        if (_timer is null)
        {
            _timer = new DispatcherTimer();
            _timer.Tick += OnTick;
        }
        _timer.Interval = interval;
        _timer.Start();
    }

    public void Stop() => _timer?.Stop();

    private void OnTick(object? sender, EventArgs args)
    {
        if (!_disposed) Tick?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        if (_timer is not null) _timer.Tick -= OnTick;
        Tick = null;
    }
}
