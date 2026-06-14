namespace MiniCompiler.Diagnostics;

public sealed record DiagnosticInfo(
    string Stage,
    string SourceName,
    string ClassName,
    SourceLocation? Location,
    string Message,
    string LineText,
    string Caret);
