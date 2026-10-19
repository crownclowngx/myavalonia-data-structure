using System.Buffers;
using System.Collections.Immutable;
using System.Text;

namespace DataStructurePlugin.Features.AhoCorasick.Domain;

/// <summary>
/// 输入解析只负责统一语义和预算，不建立自动机。提前验证全部输入，确保失败时不会返回部分结果。
/// 原始文本不做规范化，码点和原文 UTF-16 位置的映射得以保持，输出可以直接用于字符串切片。
/// </summary>
internal sealed record AcInput(ImmutableArray<string> Patterns,
    ImmutableArray<ImmutableArray<AcSymbol>> PatternSymbols, string Text, ImmutableArray<AcSymbol> Symbols)
{
    public static AcInput Parse(string keywordText, string text)
    {
        ArgumentNullException.ThrowIfNull(keywordText);
        ArgumentNullException.ThrowIfNull(text);
        if (keywordText.Length > AcLimits.MaximumKeywordCodeUnits || text.Length > AcLimits.MaximumTextCodeUnits)
            throw new ArgumentException("输入过长。词典最多 2,000 个 UTF-16 单元，文本最多 600 个 Unicode 码点。");
        var patterns = keywordText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal).ToImmutableArray();
        if (patterns.Length is 0 or > AcLimits.MaximumKeywords)
            throw new ArgumentException("请输入 1–40 个关键词，每行一个；重复词会合并，空行忽略。");
        var patternSymbols = patterns.Select(DecodeSymbols).ToImmutableArray();
        var symbols = DecodeSymbols(text);
        if (symbols.Length > AcLimits.MaximumTextSymbols)
            throw new ArgumentException("演示文本最多 600 个 Unicode 码点，请缩短文本后重试。");
        return new(patterns, patternSymbols, text, symbols);
    }

    private static ImmutableArray<AcSymbol> DecodeSymbols(string value)
    {
        var result = ImmutableArray.CreateBuilder<AcSymbol>();
        for (var offset = 0; offset < value.Length;)
        {
            // EnumerateRunes 会把非法代理序列替换成 U+FFFD；这里显式拒绝，避免悄悄改变用户输入。
            if (Rune.DecodeFromUtf16(value.AsSpan(offset), out var rune, out var length) != OperationStatus.Done)
                throw new ArgumentException("输入含不完整的 Unicode 代理字符，请修正后重试。");
            result.Add(new(rune.ToString(), offset, length));
            offset += length;
        }
        return result.ToImmutable();
    }
}
