namespace MiniCompiler.Compilation;

public sealed record SourceRepairResult(
    string SourceName,
    string OriginalText,
    string SourceText,
    IReadOnlyList<SourceCorrection> Corrections)
{
    public bool HasCorrections => Corrections.Count > 0;
}
