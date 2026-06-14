namespace MiniCompiler.Tac;

public sealed record TacInstruction(string Operation, string? Arg1 = null, string? Arg2 = null, string? Result = null)
{
    public override string ToString()
    {
        return Operation switch
        {
            "label" => $"{Result}:",
            "jmp" => $"    jmp {Result}",
            "jmp_false" => $"    jmp_false {Arg1}, {Result}",
            "decl" => $"    decl {Arg1} {Result}",
            "mov" => $"    {Result} = {Arg1}",
            "print" => $"    print {Arg1}",
            "read" => $"    read {Result}",
            "neg" or "not" => $"    {Result} = {Operation} {Arg1}",
            _ => $"    {Result} = {Arg1} {Operation} {Arg2}"
        };
    }
}
