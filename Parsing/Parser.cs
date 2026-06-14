using MiniCompiler.Ast;
using MiniCompiler.Diagnostics;
using MiniCompiler.Lexing;
using MiniCompiler.Semantics;

namespace MiniCompiler.Parsing;

public sealed class Parser
{
    private readonly string _sourceName;
    private readonly IReadOnlyList<Token> _tokens;
    private int _current;

    public Parser(string sourceName, IReadOnlyList<Token> tokens)
    {
        _sourceName = sourceName;
        _tokens = tokens;
    }

    public ProgramNode ParseProgram()
    {
        try
        {
            var statements = new List<Statement>();

            while (!IsAtEnd())
            {
                if (Match(TokenType.Newline)) continue;
                statements.Add(ParseStatement());
            }

            var location = statements.Count > 0 ? statements[0].Location : Current.Location;
            return new ProgramNode(statements, location);
        }
        catch (CompilerException) { throw; }
        catch (Exception exception)
        {
            throw CompilerException.Unexpected("Sintatico", _sourceName, nameof(Parser), Current.Location, exception);
        }
    }

    private Statement ParseStatement()
    {
        while (Match(TokenType.Newline)) { } // Pula linhas em branco

        if (Match(TokenType.Int, TokenType.Bool)) return ParseVarDeclaration(Previous);
        if (Match(TokenType.Print)) return ParsePrint(Previous.Location);
        if (Match(TokenType.Read)) return ParseRead(Previous.Location);
        if (Match(TokenType.If)) return ParseIf(Previous.Location);
        if (Match(TokenType.While)) return ParseWhile(Previous.Location);

        if (Match(TokenType.LeftBrace)) return ParseBlock(Previous.Location, false);
        if (Match(TokenType.Indent)) return ParseBlock(Previous.Location, true);

        if (Check(TokenType.Identifier) && PeekNext().Type == TokenType.Equal) return ParseAssignment();

        throw Error(Current, $"Comando inesperado perto de '{Current.Lexeme}'.");
    }

    private void ConsumeTerminator(string message)
    {
        if (Check(TokenType.Semicolon) || Check(TokenType.Newline))
        {
            Advance();
            while (Check(TokenType.Newline)) Advance(); // Consome extras
            return;
        }

        if (IsAtEnd() || Check(TokenType.RightBrace) || Check(TokenType.Dedent)) return;
        throw Error(Current, message);
    }

    private Statement ParseVarDeclaration(Token typeToken)
    {
        var declaredType = typeToken.Type == TokenType.Int ? TypeSymbol.Int : TypeSymbol.Bool;
        var name = Consume(TokenType.Identifier, "Esperava o nome da variavel.");

        Expression? initializer = null;
        if (Match(TokenType.Equal)) initializer = ParseExpression();

        ConsumeTerminator("Esperava ';' ou quebra de linha depois da declaracao.");
        return new VarDeclaration(declaredType, name.Lexeme, initializer, typeToken.Location);
    }

    private Statement ParseAssignment()
    {
        var name = Consume(TokenType.Identifier, "Esperava o nome da variavel.");
        Consume(TokenType.Equal, "Esperava '=' na atribuicao.");
        var value = ParseExpression();
        
        ConsumeTerminator("Esperava ';' ou quebra de linha depois da atribuicao.");
        return new AssignmentStatement(name.Lexeme, value, name.Location);
    }

    private Statement ParsePrint(SourceLocation location)
    {
        Consume(TokenType.LeftParen, "Esperava '(' depois de print.");
        var value = ParseExpression();
        Consume(TokenType.RightParen, "Esperava ')' depois do valor do print.");
        
        ConsumeTerminator("Esperava ';' ou quebra de linha depois do print.");
        return new PrintStatement(value, location);
    }

    private Statement ParseRead(SourceLocation location)
    {
        Consume(TokenType.LeftParen, "Esperava '(' depois de read.");
        var name = Consume(TokenType.Identifier, "Esperava o nome da variavel dentro do read.");
        Consume(TokenType.RightParen, "Esperava ')' depois da variavel do read.");
        
        ConsumeTerminator("Esperava ';' ou quebra de linha depois do read.");
        return new ReadStatement(name.Lexeme, location);
    }

    private Statement ParseIf(SourceLocation location)
    {
        bool hasParen = Match(TokenType.LeftParen);
        var condition = ParseExpression();
        if (hasParen) Consume(TokenType.RightParen, "Esperava ')' depois da condicao do if.");

        Match(TokenType.Colon); // Tolerancia Python para ':'
        while (Match(TokenType.Newline)) { }

        var thenBranch = ParseStatement();
        Statement? elseBranch = null;

        while (Match(TokenType.Newline)) { }

        if (Match(TokenType.Else))
        {
            Match(TokenType.Colon); // Tolerancia Python para ':'
            while (Match(TokenType.Newline)) { }
            elseBranch = ParseStatement();
        }

        return new IfStatement(condition, thenBranch, elseBranch, location);
    }

    private Statement ParseWhile(SourceLocation location)
    {
        bool hasParen = Match(TokenType.LeftParen);
        var condition = ParseExpression();
        if (hasParen) Consume(TokenType.RightParen, "Esperava ')' depois da condicao do while.");

        Match(TokenType.Colon); // Tolerancia Python para ':'
        while (Match(TokenType.Newline)) { }

        var body = ParseStatement();
        return new WhileStatement(condition, body, location);
    }

    private Statement ParseBlock(SourceLocation location, bool isPythonBlock)
    {
        var statements = new List<Statement>();

        while (!IsAtEnd())
        {
            if (isPythonBlock && Check(TokenType.Dedent)) break;
            if (!isPythonBlock && Check(TokenType.RightBrace)) break;

            if (Match(TokenType.Newline)) continue;

            statements.Add(ParseStatement());
        }

        if (isPythonBlock)
            Consume(TokenType.Dedent, "Esperava fim da indentacao para fechar o bloco.");
        else
            Consume(TokenType.RightBrace, "Esperava '}' para fechar o bloco.");

        return new BlockStatement(statements, location);
    }

    private Expression ParseExpression() => ParseOr();

    private Expression ParseOr()
    {
        var expression = ParseAnd();
        while (Match(TokenType.OrOr))
        {
            var operatorToken = Previous;
            var right = ParseAnd();
            expression = new BinaryExpression(expression, operatorToken.Type, right, operatorToken.Location);
        }
        return expression;
    }

    private Expression ParseAnd()
    {
        var expression = ParseEquality();
        while (Match(TokenType.AndAnd))
        {
            var operatorToken = Previous;
            var right = ParseEquality();
            expression = new BinaryExpression(expression, operatorToken.Type, right, operatorToken.Location);
        }
        return expression;
    }

    private Expression ParseEquality()
    {
        var expression = ParseComparison();
        while (Match(TokenType.EqualEqual, TokenType.BangEqual))
        {
            var operatorToken = Previous;
            var right = ParseComparison();
            expression = new BinaryExpression(expression, operatorToken.Type, right, operatorToken.Location);
        }
        return expression;
    }

    private Expression ParseComparison()
    {
        var expression = ParseTerm();
        while (Match(TokenType.Less, TokenType.LessEqual, TokenType.Greater, TokenType.GreaterEqual))
        {
            var operatorToken = Previous;
            var right = ParseTerm();
            expression = new BinaryExpression(expression, operatorToken.Type, right, operatorToken.Location);
        }
        return expression;
    }

    private Expression ParseTerm()
    {
        var expression = ParseFactor();
        while (Match(TokenType.Plus, TokenType.Minus))
        {
            var operatorToken = Previous;
            var right = ParseFactor();
            expression = new BinaryExpression(expression, operatorToken.Type, right, operatorToken.Location);
        }
        return expression;
    }

    private Expression ParseFactor()
    {
        var expression = ParseUnary();
        while (Match(TokenType.Star, TokenType.Slash, TokenType.Percent))
        {
            var operatorToken = Previous;
            var right = ParseUnary();
            expression = new BinaryExpression(expression, operatorToken.Type, right, operatorToken.Location);
        }
        return expression;
    }

    private Expression ParseUnary()
    {
        if (Match(TokenType.Bang, TokenType.Minus))
        {
            var operatorToken = Previous;
            var right = ParseUnary();
            return new UnaryExpression(operatorToken.Type, right, operatorToken.Location);
        }
        return ParsePrimary();
    }

    private Expression ParsePrimary()
    {
        if (Match(TokenType.Number, TokenType.True, TokenType.False))
            return new LiteralExpression(Previous.Literal!, Previous.Location);

        if (Match(TokenType.Identifier))
            return new VariableExpression(Previous.Lexeme, Previous.Location);

        if (Match(TokenType.LeftParen))
        {
            var location = Previous.Location;
            var expression = ParseExpression();
            Consume(TokenType.RightParen, "Esperava ')' depois da expressao.");
            return new GroupingExpression(expression, location);
        }

        throw Error(Current, $"Esperava uma expressao, mas encontrei '{Current.Lexeme}'.");
    }

    private bool Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Check(type)) { Advance(); return true; }
        }
        return false;
    }

    private Token Consume(TokenType type, string message)
    {
        if (Check(type)) return Advance();
        throw Error(Current, message);
    }

    private bool Check(TokenType type) => !IsAtEnd() && Current.Type == type;

    private Token Advance()
    {
        if (!IsAtEnd()) _current++;
        return Previous;
    }

    private bool IsAtEnd() => Current.Type == TokenType.EndOfFile;
    private Token Current => _tokens[_current];
    private Token Previous => _tokens[_current - 1];

    private Token PeekNext()
    {
        var index = Math.Min(_current + 1, _tokens.Count - 1);
        return _tokens[index];
    }

    private CompilerException Error(Token token, string message)
    {
        return new CompilerException("Sintatico", _sourceName, nameof(Parser), token.Location, message);
    }
}