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
        while (Match(TokenType.Newline)) { } 

        if (Match(TokenType.Int, TokenType.Bool)) return ParseVarDeclaration(Previous);
        if (Match(TokenType.Print)) return ParsePrint(Previous.Location);
        if (Match(TokenType.Read)) return ParseRead(Previous.Location);
        if (Match(TokenType.If)) return ParseIf(Previous.Location);
        if (Match(TokenType.While)) return ParseWhile(Previous.Location);
        if (Match(TokenType.For)) return ParseFor(Previous.Location);

        if (Match(TokenType.LeftBrace)) return ParseBlock(Previous.Location, false);
        if (Match(TokenType.Indent)) return ParseBlock(Previous.Location, true);

        if (Check(TokenType.Identifier))
        {
            if (PeekNext().Type == TokenType.Equal) return ParseAssignment();
            if (PeekNext().Type == TokenType.StarEqual) return ParseCompoundAssignment();
        }

        throw Error(Current, $"Comando inesperado perto de '{Current.Lexeme}'.");
    }

    private void ConsumeTerminator(string message)
    {
        if (Check(TokenType.Semicolon) || Check(TokenType.Newline))
        {
            Advance();
            while (Check(TokenType.Newline)) Advance(); 
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

        if (Check(TokenType.Int) && PeekNext().Type == TokenType.LeftParen)
        {
            Advance(); Advance(); 
            if (Check(TokenType.Identifier) && Current.Lexeme == "input")
            {
                Advance(); 
                Consume(TokenType.LeftParen, "");
                var prompt = ParseExpression();
                Consume(TokenType.RightParen, "");
                Consume(TokenType.RightParen, "");
                ConsumeTerminator("Esperava quebra de linha após o input.");
                
                return new BlockStatement(new List<Statement> {
                    new PrintStatement(prompt, false, name.Location),
                    new ReadStatement(name.Lexeme, name.Location)
                }, false, name.Location);
            }
        }

        var value = ParseExpression();
        ConsumeTerminator("Esperava ';' ou quebra de linha depois da atribuicao.");
        return new AssignmentStatement(name.Lexeme, value, name.Location);
    }

    private Statement ParseCompoundAssignment()
    {
        var name = Consume(TokenType.Identifier, "Esperava o nome da variavel.");
        Consume(TokenType.StarEqual, "Esperava '*='.");
        var value = ParseExpression();
        ConsumeTerminator("Esperava quebra de linha.");
        
        var bin = new BinaryExpression(new VariableExpression(name.Lexeme, name.Location), TokenType.Star, value, name.Location);
        return new AssignmentStatement(name.Lexeme, bin, name.Location);
    }

    private Statement ParsePrint(SourceLocation location)
    {
        Consume(TokenType.LeftParen, "Esperava '(' depois de print.");
        var value = ParseExpression();
        bool newLine = true;

        if (Match(TokenType.Comma))
        {
            if (Match(TokenType.Identifier) && Previous.Lexeme == "end" && Match(TokenType.Equal) && Match(TokenType.String))
                newLine = false;
        }
        
        Consume(TokenType.RightParen, "Esperava ')' depois do valor do print.");
        ConsumeTerminator("Esperava ';' ou quebra de linha depois do print.");
        return new PrintStatement(value, newLine, location);
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

        Match(TokenType.Colon); 
        while (Match(TokenType.Newline)) { }

        var thenBranch = ParseStatement();
        Statement? elseBranch = null;

        while (Match(TokenType.Newline)) { }

        if (Match(TokenType.Else))
        {
            Match(TokenType.Colon); 
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

        Match(TokenType.Colon); 
        while (Match(TokenType.Newline)) { }

        var body = ParseStatement();
        return new WhileStatement(condition, body, location);
    }

    private Statement ParseFor(SourceLocation location)
    {
        var iterVar = Consume(TokenType.Identifier, "Esperava a variavel iteradora do for.").Lexeme;
        Consume(TokenType.In, "Esperava 'in'.");
        Consume(TokenType.Range, "Esperava 'range'.");
        Consume(TokenType.LeftParen, "Esperava '('.");
        
        var start = ParseExpression();
        Consume(TokenType.Comma, "Esperava ','.");
        var end = ParseExpression();
        
        Expression step = new LiteralExpression(1, location);
        if (Match(TokenType.Comma)) step = ParseExpression();
        
        Consume(TokenType.RightParen, "Esperava ')'.");
        Match(TokenType.Colon);
        while (Match(TokenType.Newline)) { }

        var body = ParseStatement();

        bool isNegative = (step is UnaryExpression un && un.Operator == TokenType.Minus) || 
                          (step is LiteralExpression lit && lit.Value is int v && v < 0);

        var conditionOp = isNegative ? TokenType.Greater : TokenType.Less;
        var condition = new BinaryExpression(new VariableExpression(iterVar, location), conditionOp, end, location);
        var increment = new AssignmentStatement(iterVar, new BinaryExpression(new VariableExpression(iterVar, location), TokenType.Plus, step, location), location);

        var whileBodyStatements = new List<Statement>();
        if (body is BlockStatement block) whileBodyStatements.AddRange(block.Statements);
        else whileBodyStatements.Add(body);
        whileBodyStatements.Add(increment);

        return new BlockStatement(new List<Statement> {
            new AssignmentStatement(iterVar, start, location),
            new WhileStatement(condition, new BlockStatement(whileBodyStatements, false, location), location)
        }, false, location);
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
        if (isPythonBlock) Consume(TokenType.Dedent, "Esperava fim da indentacao para fechar o bloco.");
        else Consume(TokenType.RightBrace, "Esperava '}' para fechar o bloco.");
        return new BlockStatement(statements, !isPythonBlock, location);
    }

    private Expression ParseExpression() => ParseOr();
    private Expression ParseOr() { var e = ParseAnd(); while(Match(TokenType.OrOr)) e = new BinaryExpression(e, Previous.Type, ParseAnd(), Previous.Location); return e; }
    private Expression ParseAnd() { var e = ParseEquality(); while(Match(TokenType.AndAnd)) e = new BinaryExpression(e, Previous.Type, ParseEquality(), Previous.Location); return e; }
    private Expression ParseEquality() { var e = ParseComparison(); while(Match(TokenType.EqualEqual, TokenType.BangEqual)) e = new BinaryExpression(e, Previous.Type, ParseComparison(), Previous.Location); return e; }
    private Expression ParseComparison() { var e = ParseTerm(); while(Match(TokenType.Less, TokenType.LessEqual, TokenType.Greater, TokenType.GreaterEqual)) e = new BinaryExpression(e, Previous.Type, ParseTerm(), Previous.Location); return e; }
    private Expression ParseTerm() { var e = ParseFactor(); while(Match(TokenType.Plus, TokenType.Minus)) e = new BinaryExpression(e, Previous.Type, ParseFactor(), Previous.Location); return e; }
    private Expression ParseFactor() { var e = ParseUnary(); while(Match(TokenType.Star, TokenType.Slash, TokenType.Percent)) e = new BinaryExpression(e, Previous.Type, ParseUnary(), Previous.Location); return e; }
    private Expression ParseUnary() { if(Match(TokenType.Bang, TokenType.Minus)) return new UnaryExpression(Previous.Type, ParseUnary(), Previous.Location); return ParsePrimary(); }
    private Expression ParsePrimary()
    {
        if (Match(TokenType.Number, TokenType.True, TokenType.False, TokenType.String)) return new LiteralExpression(Previous.Literal!, Previous.Location);
        if (Match(TokenType.Identifier)) return new VariableExpression(Previous.Lexeme, Previous.Location);
        if (Match(TokenType.LeftParen)) { var loc = Previous.Location; var e = ParseExpression(); Consume(TokenType.RightParen, "Esperava ')'."); return new GroupingExpression(e, loc); }
        throw Error(Current, $"Esperava expressao, achou '{Current.Lexeme}'.");
    }
    private bool Match(params TokenType[] types) { foreach(var t in types) if(Check(t)) { Advance(); return true; } return false; }
    private Token Consume(TokenType t, string m) => Check(t) ? Advance() : throw Error(Current, m);
    private bool Check(TokenType t) => !IsAtEnd() && Current.Type == t;
    private Token Advance() { if(!IsAtEnd()) _current++; return Previous; }
    private bool IsAtEnd() => Current.Type == TokenType.EndOfFile;
    private Token Current => _tokens[_current];
    private Token Previous => _tokens[_current - 1];
    private Token PeekNext() => _tokens[Math.Min(_current + 1, _tokens.Count - 1)];
    private CompilerException Error(Token t, string m) => new CompilerException("Sintatico", _sourceName, nameof(Parser), t.Location, m);
}