using System.Text;

namespace MiniCompiler.Diagnostics;

public static class ErrorReporter
{
    public static void Print(Exception exception, string sourceName, string sourceText, TextWriter writer)
    {
        var error = exception as CompilerException
            ?? CompilerException.Unexpected("Geral", sourceName, "Program", null, exception);

        writer.WriteLine(Build(error, sourceText));
    }

    public static string Build(CompilerException error, string sourceText)
    {
        var diagnostic = BuildDiagnostic(error, sourceText);
        var builder = new StringBuilder();

        builder.AppendLine("ERRO ENCONTRADO");
        builder.AppendLine($"Etapa: {diagnostic.Stage}");
        builder.AppendLine($"Origem: {diagnostic.SourceName}");
        builder.AppendLine($"Classe: {diagnostic.ClassName}");

        if (diagnostic.Location is { } location)
        {
            builder.AppendLine($"Posicao: linha {location.Line}, coluna {location.Column}");

            if (!string.IsNullOrWhiteSpace(diagnostic.LineText))
            {
                builder.AppendLine($"Trecho: {diagnostic.LineText}");
                builder.AppendLine($"        {diagnostic.Caret}");
            }
        }
        else
        {
            builder.AppendLine("Posicao: entrada do projeto");
        }

        builder.AppendLine($"Mensagem: {diagnostic.Message}");

        if (error.InnerException is not null)
        {
            builder.AppendLine($"Detalhe tecnico: {error.InnerException.GetType().Name}");
        }

        return builder.ToString();
    }

    public static DiagnosticInfo BuildDiagnostic(CompilerException error, string sourceText)
    {
        var lineText = string.Empty;
        var caret = string.Empty;

        if (error.Location is { } location)
        {
            lineText = GetLine(sourceText, location.Line);

            if (!string.IsNullOrWhiteSpace(lineText))
            {
                caret = new string(' ', Math.Max(0, location.Column - 1)) + "^";
            }
        }

        return new DiagnosticInfo(
            error.Stage,
            error.SourceName,
            error.ClassName,
            error.Location,
            error.Message,
            lineText,
            caret);
    }

    public static string GetLine(string sourceText, int lineNumber)
    {
        if (lineNumber <= 0)
        {
            return string.Empty;
        }

        using var reader = new StringReader(sourceText);
        var current = 1;

        while (reader.ReadLine() is { } line)
        {
            if (current == lineNumber)
            {
                return line;
            }

            current++;
        }

        return string.Empty;
    }
}
