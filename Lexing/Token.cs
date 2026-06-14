using MiniCompiler.Diagnostics;

namespace MiniCompiler.Lexing;

public sealed record Token(TokenType Type, string Lexeme, object? Literal, SourceLocation Location)
{
    public override string ToString()
    {
        return $"{Type} '{Lexeme}' em {Location}";
    }
}
