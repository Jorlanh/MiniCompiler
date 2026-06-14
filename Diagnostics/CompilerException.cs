using System.Diagnostics;

namespace MiniCompiler.Diagnostics;

public sealed class CompilerException : Exception
{
    public CompilerException(
        string stage,
        string sourceName,
        string className,
        SourceLocation? location,
        string message,
        Exception? innerException = null,
        string? methodName = null,
        string? internalFile = null,
        int? internalLine = null)
        : base(message, innerException)
    {
        Stage = stage;
        SourceName = sourceName;
        ClassName = className;
        Location = location;
        MethodName = methodName;
        InternalFile = internalFile;
        InternalLine = internalLine;
    }

    public string Stage { get; }
    public string SourceName { get; }
    public string ClassName { get; }
    public SourceLocation? Location { get; }
    public string? MethodName { get; }
    public string? InternalFile { get; }
    public int? InternalLine { get; }

    public static CompilerException Unexpected(
        string stage,
        string sourceName,
        string fallbackClass,
        SourceLocation? location,
        Exception exception)
    {
        var stackTrace = new StackTrace(exception, true);
        var frame = stackTrace
            .GetFrames()?
            .FirstOrDefault(current => current.GetFileLineNumber() > 0)
            ?? stackTrace.GetFrames()?.FirstOrDefault();

        var method = frame?.GetMethod();
        var className = method?.DeclaringType?.Name ?? exception.TargetSite?.DeclaringType?.Name;
        var methodName = method?.Name ?? exception.TargetSite?.Name;

        if (string.IsNullOrWhiteSpace(className))
        {
            className = fallbackClass;
        }

        return new CompilerException(
            stage,
            sourceName,
            className ?? fallbackClass,
            location,
            $"Falha inesperada: {exception.Message}",
            exception,
            methodName,
            frame?.GetFileName(),
            frame?.GetFileLineNumber() > 0 ? frame.GetFileLineNumber() : null);
    }
}
