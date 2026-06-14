using System.Diagnostics;
using System.Text.Json;
using MiniCompiler.Compilation;
using MiniCompiler.Diagnostics;

namespace MiniCompiler.Python;

public sealed class PythonCompilerService
{
    private const string Script = """
        import ast
        import dis
        import io
        import json
        import platform
        import sys
        import tokenize

        path = sys.argv[1]

        def op_name(operator):
            names = {
                ast.Add: "+",
                ast.Sub: "-",
                ast.Mult: "*",
                ast.Div: "/",
                ast.Mod: "%",
                ast.Pow: "**",
            }
            return names.get(type(operator), type(operator).__name__)

        def as_text(node):
            try:
                return ast.unparse(node)
            except Exception:
                return type(node).__name__

        def emit_statement(node, lines, indent=0):
            pad = "    " * indent

            if isinstance(node, ast.Try):
                lines.append(pad + "try:")
                emit_statements(node.body, lines, indent + 1)
                for handler in node.handlers:
                    name = as_text(handler.type) if handler.type else "Exception"
                    lines.append(pad + f"except {name}:")
                    emit_statements(handler.body, lines, indent + 1)
                return

            if isinstance(node, ast.Assign):
                target = ", ".join(as_text(target) for target in node.targets)
                lines.append(pad + f"{target} = {as_text(node.value)}")
                return

            if isinstance(node, ast.AugAssign):
                target = as_text(node.target)
                lines.append(pad + f"{target} = {target} {op_name(node.op)} {as_text(node.value)}")
                return

            if isinstance(node, ast.If):
                lines.append(pad + f"if {as_text(node.test)}:")
                emit_statements(node.body, lines, indent + 1)
                if node.orelse:
                    lines.append(pad + "else:")
                    emit_statements(node.orelse, lines, indent + 1)
                return

            if isinstance(node, ast.For):
                lines.append(pad + f"for {as_text(node.target)} in {as_text(node.iter)}:")
                emit_statements(node.body, lines, indent + 1)
                return

            if isinstance(node, ast.Expr):
                lines.append(pad + as_text(node.value))
                return

            lines.append(pad + as_text(node))

        def emit_statements(nodes, lines, indent=0):
            for node in nodes:
                emit_statement(node, lines, indent)

        def count_tokens(source):
            ignored = {
                tokenize.ENCODING,
                tokenize.ENDMARKER,
                tokenize.NL,
            }

            return sum(
                1
                for token in tokenize.generate_tokens(io.StringIO(source).readline)
                if token.type not in ignored
            )

        def collect_variables(tree):
            variables = set()

            for node in ast.walk(tree):
                if isinstance(node, ast.Name) and isinstance(node.ctx, ast.Store):
                    variables.add(node.id)

            return sorted(variables)

        try:
            with open(path, "r", encoding="utf-8") as handle:
                source = handle.read()

            tree = ast.parse(source, filename=path)
            code = compile(source, path, "exec")
            intermediate = []
            emit_statements(tree.body, intermediate)
            bytecode = dis.Bytecode(code).dis()
            variables = collect_variables(tree)

            print(json.dumps({
                "ok": True,
                "tokenCount": count_tokens(source),
                "lineCount": len(source.splitlines()),
                "intermediateLines": len(intermediate),
                "astNodes": sum(1 for _ in ast.walk(tree)),
                "instructions": sum(1 for _ in dis.Bytecode(code)),
                "variables": len(variables),
                "version": platform.python_version(),
                "intermediateCode": "\n".join(intermediate),
                "bytecodeText": bytecode
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

    public PythonCompilationOutcome CompileWithRecovery(string sourceName, string sourceText)
    {
        try
        {
            return new PythonCompilationOutcome(
                Compile(sourceName, sourceText),
                sourceText,
                sourceText,
                Array.Empty<SourceCorrection>(),
                null);
        }
        catch (CompilerException exception) when (CanRepairMissingColon(exception))
        {
            var repair = PythonAutoCorrector.RepairMissingColons(sourceName, sourceText);

            if (!repair.HasCorrections)
            {
                throw;
            }

            return new PythonCompilationOutcome(
                Compile(sourceName, repair.SourceText),
                repair.SourceText,
                sourceText,
                repair.Corrections,
                exception);
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
                root.GetProperty("tokenCount").GetInt32(),
                root.GetProperty("lineCount").GetInt32(),
                root.GetProperty("intermediateLines").GetInt32(),
                root.GetProperty("astNodes").GetInt32(),
                root.GetProperty("instructions").GetInt32(),
                root.GetProperty("variables").GetInt32(),
                root.GetProperty("version").GetString() ?? "desconhecida",
                root.GetProperty("intermediateCode").GetString() ?? string.Empty,
                root.GetProperty("bytecodeText").GetString() ?? string.Empty);
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

    private static bool CanRepairMissingColon(CompilerException exception)
    {
        return exception.Stage.Equals("Python", StringComparison.OrdinalIgnoreCase)
            && exception.Message.Contains("expected ':'", StringComparison.OrdinalIgnoreCase);
    }
}
