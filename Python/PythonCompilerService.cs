using System.Diagnostics;
using System.Text.Json;
using MiniCompiler.Diagnostics;

namespace MiniCompiler.Python;

public sealed class PythonCompilerService
{
    private const string Script = """
        import ast
        import dis
        import json
        import platform
        import sys

        path = sys.argv[1]

        try:
            with open(path, "r", encoding="utf-8") as handle:
                source = handle.read()

            tree = ast.parse(source, filename=path)
            code = compile(source, path, "exec")

            print(json.dumps({
                "ok": True,
                "lineCount": len(source.splitlines()),
                "astNodes": sum(1 for _ in ast.walk(tree)),
                "instructions": sum(1 for _ in dis.Bytecode(code)),
                "version": platform.python_version()
            }))
        except SyntaxError as error:
            print(json.dumps({
                "ok": False,
                "kind": type(error).__name__,
                "message": error.msg,
                "line": error.lineno or 0,
                "column": error.offset or 0,
                "text": (error.text or "").rstrip("\n")
            }))
        except Exception as error:
            print(json.dumps({
                "ok": False,
                "kind": type(error).__name__,
                "message": str(error),
                "line": 0,
                "column": 0,
                "text": ""
            }))
        """;

    public PythonCompileResult Compile(string sourceName, string sourceText)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"minicompiler_python_{Guid.NewGuid():N}.py");

        try
        {
            File.WriteAllText(tempPath, sourceText);

            var startInfo = new ProcessStartInfo("python")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(Script);
            startInfo.ArgumentList.Add(tempPath);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                throw new CompilerException(
                    "Python",
                    sourceName,
                    nameof(PythonCompilerService),
                    null,
                    "Nao foi possivel iniciar o Python.");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(error) && string.IsNullOrWhiteSpace(output))
            {
                throw new CompilerException(
                    "Python",
                    sourceName,
                    nameof(PythonCompilerService),
                    null,
                    $"Falha ao chamar o Python: {error.Trim()}");
            }

            return ParseResult(sourceName, sourceText, output);
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CompilerException.Unexpected("Python", sourceName, nameof(PythonCompilerService), null, exception);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static PythonCompileResult ParseResult(string sourceName, string sourceText, string output)
    {
        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;
        var ok = root.GetProperty("ok").GetBoolean();

        if (ok)
        {
            return new PythonCompileResult(
                root.GetProperty("lineCount").GetInt32(),
                root.GetProperty("astNodes").GetInt32(),
                root.GetProperty("instructions").GetInt32(),
                root.GetProperty("version").GetString() ?? "desconhecida");
        }

        var line = root.GetProperty("line").GetInt32();
        var column = root.GetProperty("column").GetInt32();
        var location = line > 0
            ? new SourceLocation(line, Math.Max(1, column), 0)
            : (SourceLocation?)null;

        var kind = root.GetProperty("kind").GetString() ?? "SyntaxError";
        var message = root.GetProperty("message").GetString() ?? "Erro de sintaxe em Python.";

        throw new CompilerException(
            "Python",
            sourceName,
            nameof(PythonCompilerService),
            location,
            $"{kind}: {message}");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Arquivo temporario: se ficar bloqueado, o sistema limpa depois.
        }
    }
}
