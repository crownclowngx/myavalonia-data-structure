using System.Collections.Immutable;

namespace DataStructurePlugin.Features.AhoCorasick.Content;

/// <summary>可重复载入的课堂输入。字符串不可变，预设不持有任何 Document 的可变会话状态。</summary>
public sealed record ExampleCase(string Name, string Keywords, string Text)
{
    public override string ToString() => Name;
}

public static class AcExampleCatalog
{
    public static ImmutableArray<ExampleCase> All { get; } = [
        new("经典 · ushers", "he\nshe\nhis\nhers", "ushers"),
        new("重叠 · a / aa / aaa", "a\naa\naaa", "aaaaa"),
        new("中文 · 北京大学", "北京\n北京大学\n大学", "我在北京大学学习"),
        new("连续回退 · 多层后缀", "a\naa\naaa\naaaab\nb", "aaaacaaaab"),
        new("Unicode · 🙂", "🙂\n🙂a\na", "🙂a🙂"),
        new("没有命中", "he\nshe", "xyz")
    ];
}
