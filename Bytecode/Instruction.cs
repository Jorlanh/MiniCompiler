using MiniCompiler.Diagnostics;

namespace MiniCompiler.Bytecode;

public sealed record Instruction(OpCode Code, object? Operand, SourceLocation Location)
{
    public override string ToString()
    {
        return Operand is null ? Code.ToString() : $"{Code} {Operand}";
    }
}
