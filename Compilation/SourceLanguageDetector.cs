namespace MiniCompiler.Compilation;

public static class SourceLanguageDetector
{
    public static bool IsPython(string sourceName, string sourceText)
    {
        if (Path.GetExtension(sourceName).Equals(".py", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var lines = sourceText.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimStart();

            if (line.StartsWith("try:", StringComparison.Ordinal)
                || line.StartsWith("except ", StringComparison.Ordinal)
                || line.StartsWith("def ", StringComparison.Ordinal)
                || line.StartsWith("import ", StringComparison.Ordinal)
                || line.StartsWith("from ", StringComparison.Ordinal)
                || line.StartsWith("for ", StringComparison.Ordinal) && line.EndsWith(':')
                || line.Contains("input(", StringComparison.Ordinal)
                || line.Contains("range(", StringComparison.Ordinal)
                || line.Contains("print(f\"", StringComparison.Ordinal)
                || line.Contains("print(f'", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
