using MiniCompiler.Compilation;
using MiniCompiler.Diagnostics;

namespace MiniCompiler.Python;

public sealed record PythonCompilationOutcome(
    PythonCompileResult Result,
    string SourceText,
    string OriginalSourceText,
    IReadOnlyList<SourceCorrection> Corrections,
    CompilerException? OriginalError)
{
    public bool WasRepaired => Corrections.Count > 0 && OriginalError is not null;
}
