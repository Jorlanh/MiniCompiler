using MiniCompiler.Ast;
using MiniCompiler.Bytecode;
using MiniCompiler.Lexing;
using MiniCompiler.Tac;

namespace MiniCompiler.Compilation;

public sealed record CompilationResult(
    IReadOnlyList<Token> Tokens,
    ProgramNode Program,
    IReadOnlyList<TacInstruction> Tac,
    BytecodeProgram Bytecode);
