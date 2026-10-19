using DataStructurePlugin.Features.AhoCorasick.Application;
using DataStructurePlugin.Features.AhoCorasick.Domain;
using Xunit;

namespace DataStructurePlugin.Tests;

public sealed class AcPlaybackSessionTests
{
    private static AcTrace Trace() => new AhoCorasickTraceBuilder().Build("he\nshe", "she");

    [Fact]
    public void 播放暂停终点重播和释放均有确定行为()
    {
        var clock = new TestPlaybackClock();
        using var playback = new AcPlaybackSession(clock);
        playback.Load(Trace());
        playback.TogglePlayback();
        Assert.True(playback.IsPlaying);
        Assert.Equal(TimeSpan.FromMilliseconds(900), clock.Interval);
        clock.Fire();
        Assert.Equal(1, playback.FrameIndex);
        var pending = clock.CapturePendingTick();
        playback.Pause();
        pending();
        Assert.Equal(1, playback.FrameIndex);
        Assert.False(clock.Running);
        playback.Seek(playback.MaximumPosition - 1);
        playback.TogglePlayback();
        clock.Fire();
        Assert.Equal(playback.MaximumPosition, playback.FrameIndex);
        Assert.False(playback.IsPlaying);
        playback.TogglePlayback();
        Assert.Equal(0, playback.FrameIndex);
        Assert.True(playback.IsPlaying);
        pending = clock.CapturePendingTick();
        playback.Dispose();
        pending();
        playback.TogglePlayback();
        playback.Seek(9);
        playback.Position = 5;
        Assert.Equal(0, playback.FrameIndex);
        Assert.True(clock.Disposed);
        Assert.False(playback.IsPlaying);
        Assert.Throws<ObjectDisposedException>(() => playback.Load(Trace()));
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(double.NegativeInfinity, 0)]
    [InlineData(-100, 0)]
    [InlineData(2.6, 3)]
    public void 滑块拒绝非有限值并规范化有限值(double position, int expected)
    {
        using var playback = new AcPlaybackSession(new TestPlaybackClock());
        playback.Load(Trace());
        playback.Position = position;
        Assert.Equal(expected, playback.FrameIndex);
        playback.Position = double.MaxValue;
        Assert.Equal(playback.MaximumPosition, playback.FrameIndex);
    }

    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(double.NegativeInfinity, 1)]
    [InlineData(0, 0.5)]
    [InlineData(-1, 0.5)]
    [InlineData(10, 4)]
    [InlineData(2, 2)]
    public void 倍速不会向时钟传播非法间隔(double value, double expected)
    {
        var clock = new TestPlaybackClock();
        using var playback = new AcPlaybackSession(clock);
        playback.Load(Trace());
        playback.TogglePlayback();
        playback.Speed = value;
        Assert.Equal(expected, playback.Speed);
        Assert.Equal(TimeSpan.FromMilliseconds(900 / expected), clock.Interval);
        Assert.True(clock.Interval > TimeSpan.Zero);
    }

    [Fact]
    public void 相同滑块回写不会暂停自动播放而手动跳转会暂停()
    {
        var clock = new TestPlaybackClock();
        using var playback = new AcPlaybackSession(clock);
        playback.Load(Trace());
        playback.TogglePlayback();
        playback.Position = playback.Position;
        Assert.True(playback.IsPlaying);
        playback.Position = 4;
        Assert.Equal(4, playback.FrameIndex);
        Assert.False(playback.IsPlaying);
        clock.Fire();
        Assert.Equal(4, playback.FrameIndex);
    }

    [Fact]
    public void 新轨迹载入后旧节拍不得推进新轨迹()
    {
        var clock = new TestPlaybackClock();
        using var playback = new AcPlaybackSession(clock);
        playback.Load(Trace());
        playback.TogglePlayback();
        var pending = clock.CapturePendingTick();
        var replacement = new AhoCorasickTraceBuilder().Build("北京", "北京");
        playback.Load(replacement);
        pending();
        Assert.Same(replacement, playback.Trace);
        Assert.Equal(0, playback.FrameIndex);
        Assert.False(playback.IsPlaying);
        Assert.Throws<ArgumentException>(() => playback.Load(replacement with { Steps = [] }));
        Assert.Same(replacement, playback.Trace);
    }
}
