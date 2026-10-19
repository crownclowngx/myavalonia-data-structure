namespace DataStructurePlugin.Features.AhoCorasick.Domain;

/// <summary>
/// 教学实验的资源预算。输入解析与构建过程共同依赖这些规则，不让解析器反向依赖某个构建器实现。
/// 限制针对完整教学轨迹及可读画布，不代表 AC 算法在工程应用中的规模上限。
/// </summary>
public static class AcLimits
{
    public const int MaximumKeywords = 40;
    public const int MaximumKeywordCodeUnits = 2_000;
    public const int MaximumTextCodeUnits = 2_400;
    public const int MaximumTextSymbols = 600;
    public const int MaximumNodes = 160;
    public const int MaximumSteps = 12_000;
}
