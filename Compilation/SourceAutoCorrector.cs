using System.Text;
using MiniCompiler.Diagnostics;

namespace MiniCompiler.Compilation;

public static class SourceAutoCorrector
{
    public static SourceRepairResult Repair(string sourceName, string sourceText)
    {
        try
        {
            var normalized = sourceText.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = normalized.Split('\n');
            var fixedLines = new List<string>(lines.Length + 4);
            var corrections = new List<SourceCorrection>();
            var openBraces = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var originalLine = lines[i];
                var fixedLine = FixMissingSemicolon(originalLine, i + 1, corrections);

                openBraces += CountChar(RemoveLineComment(fixedLine), '{');
                openBraces -= CountChar(RemoveLineComment(fixedLine), '}');
                fixedLines.Add(fixedLine);
            }

            var output = new StringBuilder(string.Join(Environment.NewLine, fixedLines));
            var lastLine = Math.Max(1, lines.Length);

            for (var i = 0; i < openBraces; i++)
            {
                if (output.Length > 0)
                {
                    output.AppendLine();
                }

                output.Append('}');
                corrections.Add(new SourceCorrection(
                    $"Faltou '}}' para fechar um bloco aberto. Fechamento inserido no final do arquivo.",
                    new SourceLocation(lastLine + i + 1, 1, sourceText.Length),
                    string.Empty,
                    "}"));
            }

            return new SourceRepairResult(sourceName, sourceText, output.ToString(), corrections);
        }
        catch
        {
            return new SourceRepairResult(sourceName, sourceText, sourceText, Array.Empty<SourceCorrection>());
        }
    }

    private static string FixMissingSemicolon(string line, int lineNumber, List<SourceCorrection> corrections)
    {
        var fixedLine = FixPackedStatement(line, lineNumber, corrections);
        var codePart = RemoveLineComment(fixedLine).TrimEnd();

        if (!NeedsSemicolon(codePart))
        {
            return fixedLine;
        }

        var commentIndex = fixedLine.IndexOf("//", StringComparison.Ordinal);
        string correctedLine;
        int column;

        if (commentIndex >= 0)
        {
            var beforeComment = fixedLine[..commentIndex].TrimEnd();
            var afterComment = fixedLine[commentIndex..];
            column = beforeComment.Length + 1;
            correctedLine = beforeComment + "; " + afterComment;
        }
        else
        {
            column = fixedLine.Length + 1;
            correctedLine = fixedLine.TrimEnd() + ";";
        }

        corrections.Add(new SourceCorrection(
            $"Faltou ';' na linha {lineNumber}. O ponto e virgula foi inserido automaticamente.",
            new SourceLocation(lineNumber, column, 0),
            fixedLine,
            correctedLine));

        return correctedLine;
    }

    private static string FixPackedStatement(string line, int lineNumber, List<SourceCorrection> corrections)
    {
        var codePart = RemoveLineComment(line);
        var insertAt = FindNextStatementStart(codePart);

        if (insertAt <= 0)
        {
            return line;
        }

        var prefix = codePart[..insertAt].TrimEnd();

        if (!CanEndWithSemicolon(prefix))
        {
            return line;
        }

        var correctedLine = line[..insertAt].TrimEnd() + "; " + line[insertAt..].TrimStart();

        corrections.Add(new SourceCorrection(
            $"Faltou ';' antes de outro comando na linha {lineNumber}. O separador foi inserido automaticamente.",
            new SourceLocation(lineNumber, prefix.Length + 1, 0),
            line,
            correctedLine));

        return correctedLine;
    }

    private static bool NeedsSemicolon(string codePart)
    {
        if (string.IsNullOrWhiteSpace(codePart))
        {
            return false;
        }

        if (codePart.EndsWith(';') || codePart.EndsWith('{') || codePart.EndsWith('}'))
        {
            return false;
        }

        if (codePart.StartsWith("if", StringComparison.Ordinal)
            || codePart.StartsWith("while", StringComparison.Ordinal)
            || codePart.StartsWith("else", StringComparison.Ordinal))
        {
            return false;
        }

        return codePart.StartsWith("int ", StringComparison.Ordinal)
            || codePart.StartsWith("bool ", StringComparison.Ordinal)
            || codePart.StartsWith("print(", StringComparison.Ordinal)
            || codePart.StartsWith("read(", StringComparison.Ordinal)
            || LooksLikeAssignment(codePart);
    }

    private static bool LooksLikeAssignment(string codePart)
    {
        var equalIndex = codePart.IndexOf('=', StringComparison.Ordinal);

        if (equalIndex <= 0)
        {
            return false;
        }

        if (codePart.Contains("==", StringComparison.Ordinal)
            || codePart.Contains("<=", StringComparison.Ordinal)
            || codePart.Contains(">=", StringComparison.Ordinal)
            || codePart.Contains("!=", StringComparison.Ordinal))
        {
            return false;
        }

        var first = codePart[0];
        return char.IsLetter(first) || first == '_';
    }

    private static bool CanEndWithSemicolon(string codePart)
    {
        return codePart.StartsWith("int ", StringComparison.Ordinal)
            || codePart.StartsWith("bool ", StringComparison.Ordinal)
            || codePart.StartsWith("print(", StringComparison.Ordinal)
            || codePart.StartsWith("read(", StringComparison.Ordinal)
            || LooksLikeAssignment(codePart);
    }

    private static int FindNextStatementStart(string codePart)
    {
        var candidates = new[]
        {
            " print(",
            " read(",
            " int ",
            " bool "
        };

        var best = -1;

        foreach (var candidate in candidates)
        {
            var index = codePart.IndexOf(candidate, StringComparison.Ordinal);

            if (index > 0 && (best < 0 || index < best))
            {
                best = index;
            }
        }

        return best;
    }

    private static string RemoveLineComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index >= 0 ? line[..index] : line;
    }

    private static int CountChar(string value, char expected)
    {
        var count = 0;

        foreach (var current in value)
        {
            if (current == expected)
            {
                count++;
            }
        }

        return count;
    }
}
