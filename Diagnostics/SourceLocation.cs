namespace MiniCompiler.Diagnostics;

public readonly record struct SourceLocation(int Line, int Column, int Index)
{
    public override string ToString()
    {
        return $"linha {Line}, coluna {Column}";
    }
}
