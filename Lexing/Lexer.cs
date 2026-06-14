using MiniCompiler.Diagnostics;

namespace MiniCompiler.Lexing;

public sealed class Lexer
{
    private static readonly Dictionary<string, TokenType> ReservedWords = new()
    {
        ["int"] = TokenType.Int,
        ["bool"] = TokenType.Bool,
        ["if"] = TokenType.If,
        ["else"] = TokenType.Else,
        ["elif"] = TokenType.Else, 
        ["while"] = TokenType.While,
        ["for"] = TokenType.For,
        ["in"] = TokenType.In,
        ["range"] = TokenType.Range,
        ["print"] = TokenType.Print,
        ["read"] = TokenType.Read,
        ["true"] = TokenType.True,
        ["false"] = TokenType.False,
        ["True"] = TokenType.True,   
        ["False"] = TokenType.False, 
        ["def"] = TokenType.Def,     
        ["and"] = TokenType.AndAnd,  
        ["or"] = TokenType.OrOr,     
        ["not"] = TokenType.Bang     
    };

    private readonly string _sourceName;
    private readonly string _source;
    private readonly List<Token> _tokens = new();
    private readonly Stack<int> _indentations = new();

    private int _start;
    private int _current;
    private int _line = 1;
    private int _column = 1;
    private int _startLine = 1;
    private int _startColumn = 1;
    private bool _isAtLineStart = true;

    public Lexer(string sourceName, string source)
    {
        _sourceName = sourceName;
        _source = source;
        _indentations.Push(0); 
    }

    public IReadOnlyList<Token> ScanTokens()
    {
        try
        {
            while (!IsAtEnd())
            {
                _start = _current;
                _startLine = _line;
                _startColumn = _column;

                if (_isAtLineStart)
                {
                    int spaces = 0;
                    while (!IsAtEnd() && (Peek() == ' ' || Peek() == '\t'))
                    {
                        spaces += Peek() == '\t' ? 4 : 1;
                        Advance();
                    }

                    if (IsAtEnd() || Peek() == '\n' || Peek() == '\r' || Peek() == '#' || (Peek() == '/' && PeekNext() == '/'))
                    {
                        // Ignore
                    }
                    else
                    {
                        if (spaces > _indentations.Peek())
                        {
                            _indentations.Push(spaces);
                            AddToken(TokenType.Indent);
                        }
                        else if (spaces < _indentations.Peek())
                        {
                            while (_indentations.Count > 0 && _indentations.Peek() > spaces)
                            {
                                _indentations.Pop();
                                AddToken(TokenType.Dedent);
                            }
                        }
                        _isAtLineStart = false;
                    }

                    _start = _current;
                    _startLine = _line;
                    _startColumn = _column;
                    if (IsAtEnd()) break;
                }

                ScanToken();
            }

            _start = _current;
            while (_indentations.Count > 1)
            {
                _indentations.Pop();
                AddToken(TokenType.Dedent);
            }

            _tokens.Add(new Token(
                TokenType.EndOfFile,
                string.Empty,
                null,
                new SourceLocation(_line, _column, _current)));

            return _tokens;
        }
        catch (CompilerException) { throw; }
        catch (Exception exception)
        {
            throw CompilerException.Unexpected("Lexico", _sourceName, nameof(Lexer), new SourceLocation(_startLine, _startColumn, _start), exception);
        }
    }

    private void ScanToken()
    {
        var character = Advance();

        switch (character)
        {
            case '(': AddToken(TokenType.LeftParen); break;
            case ')': AddToken(TokenType.RightParen); break;
            case '{': AddToken(TokenType.LeftBrace); break;
            case '}': AddToken(TokenType.RightBrace); break;
            case ';': AddToken(TokenType.Semicolon); break;
            case ',': AddToken(TokenType.Comma); break;
            case '+': AddToken(TokenType.Plus); break;
            case '-': AddToken(TokenType.Minus); break;
            case '*': AddToken(Match('=') ? TokenType.StarEqual : TokenType.Star); break;
            case '%': AddToken(TokenType.Percent); break;
            case ':': AddToken(TokenType.Colon); break; 
            case '!': AddToken(Match('=') ? TokenType.BangEqual : TokenType.Bang); break;
            case '=': AddToken(Match('=') ? TokenType.EqualEqual : TokenType.Equal); break;
            case '<': AddToken(Match('=') ? TokenType.LessEqual : TokenType.Less); break;
            case '>': AddToken(Match('=') ? TokenType.GreaterEqual : TokenType.Greater); break;
            case '&':
                if (Match('&')) { AddToken(TokenType.AndAnd); break; }
                Fail("Use && para operador logico E."); break;
            case '|':
                if (Match('|')) { AddToken(TokenType.OrOr); break; }
                Fail("Use || para operador logico OU."); break;
            case '#': 
                while (Peek() != '\n' && !IsAtEnd()) Advance();
                break;
            case '"':
                StringLiteral(false);
                break;
            case 'f':
                if (Peek() == '"')
                {
                    Advance();
                    StringLiteral(true);
                }
                else
                {
                    Identifier();
                }
                break;
            case '/':
                if (Match('/'))
                {
                    while (Peek() != '\n' && !IsAtEnd()) Advance();
                }
                else if (Match('*'))
                {
                    BlockComment();
                }
                else
                {
                    AddToken(TokenType.Slash);
                }
                break;
            case '\n':
                _line++;
                _column = 1;
                _isAtLineStart = true;
                AddToken(TokenType.Newline);
                break;
            case '\r':
            case ' ':
            case '\t':
                break; 
            default:
                if (char.IsDigit(character)) Number();
                else if (IsIdentifierStart(character)) Identifier();
                else Fail($"Caractere inesperado '{character}'.");
                break;
        }
    }

    private void StringLiteral(bool isFString)
    {
        while (Peek() != '"' && !IsAtEnd()) Advance();
        
        if (IsAtEnd()) Fail("String nao fechada.");
        Advance(); 

        var value = _source[_start.._current];
        value = isFString ? value.Substring(2, value.Length - 3) : value.Substring(1, value.Length - 2);
        value = value.Replace("\\n", "\n");
        
        AddToken(TokenType.String, value);
    }

    private void Identifier()
    {
        while (IsIdentifierPart(Peek())) Advance();

        var text = _source[_start.._current];
        var type = ReservedWords.TryGetValue(text, out var reservedType) ? reservedType : TokenType.Identifier;

        object? literal = type switch
        {
            TokenType.True => true,
            TokenType.False => false,
            _ => null
        };

        AddToken(type, literal);
    }

    private void Number()
    {
        while (char.IsDigit(Peek())) Advance();
        var text = _source[_start.._current];
        if (!int.TryParse(text, out var value)) Fail($"Numero inteiro fora do limite permitido: {text}.");
        AddToken(TokenType.Number, value);
    }

    private void BlockComment()
    {
        while (!IsAtEnd())
        {
            if (Peek() == '*' && PeekNext() == '/') { Advance(); Advance(); return; }
            Advance();
        }
        Fail("Comentario de bloco nao foi fechado.");
    }

    private char Advance()
    {
        var character = _source[_current];
        _current++;
        if (character != '\n') _column++;
        return character;
    }

    private bool Match(char expected)
    {
        if (IsAtEnd() || _source[_current] != expected) return false;
        Advance(); return true;
    }

    private char Peek() => IsAtEnd() ? '\0' : _source[_current];
    private char PeekNext() => _current + 1 >= _source.Length ? '\0' : _source[_current + 1];
    private bool IsAtEnd() => _current >= _source.Length;

    private void AddToken(TokenType type, object? literal = null)
    {
        var text = _source[_start.._current];
        _tokens.Add(new Token(type, text, literal, new SourceLocation(_startLine, _startColumn, _start)));
    }

    private void Fail(string message) => throw new CompilerException("Lexico", _sourceName, nameof(Lexer), new SourceLocation(_startLine, _startColumn, _start), message);

    private static bool IsIdentifierStart(char character) => char.IsLetter(character) || character == '_';
    private static bool IsIdentifierPart(char character) => char.IsLetterOrDigit(character) || character == '_';
}