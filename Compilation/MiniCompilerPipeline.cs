using MiniCompiler.Bytecode;
using MiniCompiler.Diagnostics;
using MiniCompiler.Lexing;
using MiniCompiler.Parsing;
using MiniCompiler.Semantics;
using MiniCompiler.Tac;

namespace MiniCompiler.Compilation;

public sealed class MiniCompilerPipeline
{
    public CompilationResult Compile(string sourceName, string sourceText)
    {
        try
        {
            var lexer = new Lexer(sourceName, sourceText);
            var tokens = lexer.ScanTokens();

            var parser = new Parser(sourceName, tokens);
            var program = parser.ParseProgram();

            var semanticAnalyzer = new SemanticAnalyzer(sourceName);
            semanticAnalyzer.Analyze(program);

            var tac = new TacGenerator(sourceName).Generate(program);
            var bytecode = new BytecodeEmitter(sourceName, semanticAnalyzer.VariableTypes).Emit(program);

            return new CompilationResult(tokens, program, tac, bytecode);
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CompilerException.Unexpected(
                "Pipeline",
                sourceName,
                nameof(MiniCompilerPipeline),
                null,
                exception);
        }
    }
}
