using DataStructurePlugin.Features.AhoCorasick.Domain;

namespace DataStructurePlugin.Features.AhoCorasick.Application;

/// <summary>
/// 只管理一份轨迹的游标与播放生命周期。它不构建自动机，不读取输入框，也不格式化教学内容。
/// 游标内部始终为整数；对外接受 double 是为了适配滑块，并集中处理 NaN、无穷和越界值。
/// 这是一个普通的会话对象，使用构造注入的小型时钟，不引入状态模式或继承层次。
/// </summary>
public sealed class AcPlaybackSession : IDisposable
{
    private readonly IAcPlaybackClock _clock;
    private bool _disposed;
    private double _speed = 1;
    public AcPlaybackSession(IAcPlaybackClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _clock.Tick += OnTick;
    }

    public event EventHandler? Changed;
    public AcTrace Trace { get; private set; } = null!;
    public int FrameIndex { get; private set; }
    public int MaximumPosition => Trace is null ? 0 : Trace.Steps.Length - 1;
    public bool IsPlaying { get; private set; }
    public AcStep CurrentStep => Trace.Steps[FrameIndex];
    public double Position
    {
        get => FrameIndex;
        set
        {
            // 非有限值没有对应的算法步骤；忽略而不是把它传播到数组索引或定时器中。
            if (_disposed || !double.IsFinite(value)) return;
            var index = (int)Math.Clamp(Math.Round(value), 0, MaximumPosition);
            if (index != FrameIndex) Seek(index);
        }
    }
    public double Speed
    {
        get => _speed;
        set
        {
            if (_disposed || !double.IsFinite(value)) return;
            var normalized = Math.Clamp(value, 0.5, 4);
            if (_speed == normalized) return;
            _speed = normalized;
            if (IsPlaying) _clock.Start(Interval);
            Notify();
        }
    }
    private TimeSpan Interval => TimeSpan.FromMilliseconds(900 / _speed);

    /// <summary>在调用方完成新实验的其他投影后替换轨迹。载入总会暂停并回到起点。</summary>
    public void Load(AcTrace trace)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(trace);
        if (trace.Steps.IsDefaultOrEmpty) throw new ArgumentException("轨迹必须至少包含一个步骤。", nameof(trace));
        StopCore();
        Trace = trace;
        FrameIndex = 0;
        Notify();
    }

    public void Seek(int index)
    {
        if (_disposed || Trace is null) return;
        StopCore();
        FrameIndex = Math.Clamp(index, 0, MaximumPosition);
        Notify();
    }

    public void TogglePlayback()
    {
        if (_disposed || Trace is null) return;
        if (IsPlaying) { Pause(); return; }
        if (FrameIndex == MaximumPosition) FrameIndex = 0;
        _clock.Start(Interval);
        IsPlaying = true;
        Notify();
    }

    public void Pause()
    {
        if (_disposed) return;
        var wasPlaying = IsPlaying;
        StopCore();
        if (wasPlaying) Notify();
    }

    private void OnTick(object? sender, EventArgs args)
    {
        // Stop 只能阻止后续计时，不能撤回已经排队的回调；本地状态才是是否推进的最终依据。
        if (_disposed || !IsPlaying) return;
        FrameIndex = Math.Min(FrameIndex + 1, MaximumPosition);
        if (FrameIndex == MaximumPosition) StopCore();
        Notify();
    }

    private void StopCore()
    {
        _clock.Stop();
        IsPlaying = false;
    }

    private void Notify() => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopCore();
        _clock.Tick -= OnTick;
        _clock.Dispose();
        Changed = null;
    }
}
