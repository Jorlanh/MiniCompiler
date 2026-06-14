using MiniCompiler.Diagnostics;

namespace MiniCompiler.Compilation;

public sealed record SourceCorrection(
    string Message,
    SourceLocation Location,
    string OriginalLine,
    string CorrectedLine);
