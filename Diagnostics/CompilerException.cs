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
        Exception? innerException = null)
        : base(message, innerException)
    {
        Stage = stage;
        SourceName = sourceName;
        ClassName = className;
        Location = location;
    }

    public string Stage { get; }
    public string SourceName { get; }
    public string ClassName { get; }
    public SourceLocation? Location { get; }

    public static CompilerException Unexpected(
        string stage,
        string sourceName,
        string fallbackClass,
        SourceLocation? location,
        Exception exception)
    {
        var className = exception.TargetSite?.DeclaringType?.Name;

        if (string.IsNullOrWhiteSpace(className))
        {
            var frame = new StackTrace(exception, true).GetFrames()?.FirstOrDefault();
            className = frame?.GetMethod()?.DeclaringType?.Name;
        }

        return new CompilerException(
            stage,
            sourceName,
            className ?? fallbackClass,
            location,
            $"Falha inesperada: {exception.Message}",
            exception);
    }
}
