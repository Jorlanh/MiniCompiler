using MiniCompiler.Semantics;

namespace MiniCompiler.Bytecode;

public sealed record BytecodeProgram(IReadOnlyList<Instruction> Instructions, IReadOnlyList<TypeSymbol> VariableTypes);
