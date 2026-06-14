namespace MiniCompiler.Lexing;

public enum TokenType
{
    EndOfFile,
    Identifier,
    Number,

    // Suporte Python/Mini
    Indent,
    Dedent,
    Newline,
    Colon,
    Def,

    Int,
    Bool,
    If,
    Else,
    While,
    Print,
    Read,
    True,
    False,

    LeftParen,
    RightParen,
    LeftBrace,
    RightBrace,
    Semicolon,
    Comma,

    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    Bang,
    BangEqual,
    Equal,
    EqualEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    AndAnd,
    OrOr
}