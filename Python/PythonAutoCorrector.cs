using System.Text;
using MiniCompiler.Compilation;
using MiniCompiler.Diagnostics;

namespace MiniCompiler.Python;

public static class PythonAutoCorrector
{
    public static SourceRepairResult RepairMissingColons(string sourceName, string sourceText)
    {
        try
        {
            var normalized = sourceText.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = normalized.Split('\n');
            var fixedLines = new List<string>(lines.Length);
            var corrections = new List<SourceCorrection>();

            for (var index = 0; index < lines.Length; index++)
            {
                fixedLines.Add(FixLine(lines[index], index + 1, corrections));
            }

            return new SourceRepairResult(
                sourceName,
                sourceText,
                string.Join(Environment.NewLine, fixedLines),
                corrections);
        }
        catch
        {
            return new SourceRepairResult(sourceName, sourceText, sourceText, Array.Empty<SourceCorrection>());
        }
    }

    private static string FixLine(string line, int lineNumber, List<SourceCorrection> corrections)
    {
        var codePart = RemoveComment(line).Trim();

        if (!NeedsColon(codePart))
        {
            return line;
        }

        var commentIndex = line.IndexOf('#');
        string fixedLine;
        int column;

        if (commentIndex >= 0)
        {
            var beforeComment = line[..commentIndex].TrimEnd();
            fixedLine = beforeComment + ": " + line[commentIndex..];
            column = beforeComment.Length + 1;
        }
        else
        {
            fixedLine = line.TrimEnd() + ":";
            column = line.TrimEnd().Length + 1;
        }

        corrections.Add(new SourceCorrection(
            $"Faltou ':' na linha {lineNumber}. O dois-pontos foi inserido automaticamente.",
            new SourceLocation(lineNumber, column, 0),
            line,
            fixedLine));

        return fixedLine;
    }

    private static bool NeedsColon(string codePart)
    {
        if (string.IsNullOrWhiteSpace(codePart) || codePart.EndsWith(':'))
        {
            return false;
        }

        return StartsBlock(codePart, "if ")
            || StartsBlock(codePart, "elif ")
            || codePart == "else"
            || StartsBlock(codePart, "for ")
            || StartsBlock(codePart, "while ")
            || codePart == "try"
            || StartsBlock(codePart, "except")
            || codePart == "finally"
            || StartsBlock(codePart, "with ")
            || StartsBlock(codePart, "def ")
            || StartsBlock(codePart, "class ");
    }

    private static bool StartsBlock(string codePart, string keyword)
    {
        return codePart.StartsWith(keyword, StringComparison.Ordinal);
    }

    private static string RemoveComment(string line)
    {
        var builder = new StringBuilder();
        var quote = '\0';
        var escaped = false;

        foreach (var current in line)
        {
            if (escaped)
            {
                builder.Append(current);
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                builder.Append(current);
                escaped = true;
                continue;
            }

            if (quote != '\0')
            {
                builder.Append(current);

                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
                builder.Append(current);
                continue;
            }

            if (current == '#')
            {
                break;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}
