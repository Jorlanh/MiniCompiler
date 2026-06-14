using MiniCompiler.Ast;
using MiniCompiler.Diagnostics;
using MiniCompiler.Lexing;
using MiniCompiler.Semantics;

namespace MiniCompiler.Tac;

public sealed class TacGenerator : IStatementVisitor<object?>, IExpressionVisitor<string>
{
    private readonly string _sourceName;
    private readonly List<TacInstruction> _instructions = new();
    private int _tempCounter;
    private int _labelCounter;

    public TacGenerator(string sourceName)
    {
        _sourceName = sourceName;
    }

    public IReadOnlyList<TacInstruction> Generate(ProgramNode program)
    {
        try
        {
            foreach (var statement in program.Statements)
            {
                statement.Accept(this);
            }

            return _instructions;
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CompilerException.Unexpected(
                "TAC",
                _sourceName,
                nameof(TacGenerator),
                program.Location,
                exception);
        }
    }

    public object? VisitVarDeclaration(VarDeclaration statement)
    {
        var target = Variable(statement.Symbol);
        _instructions.Add(new TacInstruction("decl", Format(statement.DeclaredType), null, target));

        if (statement.Initializer is not null)
        {
            var value = statement.Initializer.Accept(this);
            _instructions.Add(new TacInstruction("mov", value, null, target));
        }

        return null;
    }

    public object? VisitAssignment(AssignmentStatement statement)
    {
        var value = statement.Value.Accept(this);
        _instructions.Add(new TacInstruction("mov", value, null, Variable(statement.Symbol)));
        return null;
    }

    public object? VisitPrint(PrintStatement statement)
    {
        var value = statement.Value.Accept(this);
        _instructions.Add(new TacInstruction("print", value));
        return null;
    }

    public object? VisitRead(ReadStatement statement)
    {
        _instructions.Add(new TacInstruction("read", null, null, Variable(statement.Symbol)));
        return null;
    }

    public object? VisitIf(IfStatement statement)
    {
        var elseLabel = NewLabel();
        var endLabel = NewLabel();
        var condition = statement.Condition.Accept(this);

        _instructions.Add(new TacInstruction("jmp_false", condition, null, elseLabel));
        statement.ThenBranch.Accept(this);
        _instructions.Add(new TacInstruction("jmp", null, null, endLabel));
        _instructions.Add(new TacInstruction("label", null, null, elseLabel));
        statement.ElseBranch?.Accept(this);
        _instructions.Add(new TacInstruction("label", null, null, endLabel));
        return null;
    }

    public object? VisitWhile(WhileStatement statement)
    {
        var startLabel = NewLabel();
        var endLabel = NewLabel();

        _instructions.Add(new TacInstruction("label", null, null, startLabel));
        var condition = statement.Condition.Accept(this);
        _instructions.Add(new TacInstruction("jmp_false", condition, null, endLabel));
        statement.Body.Accept(this);
        _instructions.Add(new TacInstruction("jmp", null, null, startLabel));
        _instructions.Add(new TacInstruction("label", null, null, endLabel));
        return null;
    }

    public object? VisitBlock(BlockStatement statement)
    {
        foreach (var childStatement in statement.Statements)
        {
            childStatement.Accept(this);
        }

        return null;
    }

    public string VisitLiteral(LiteralExpression expression)
    {
        return expression.Value switch
        {
            bool value => value ? "#true" : "#false",
            int value => $"#{value}",
            _ => throw new CompilerException(
                "TAC",
                _sourceName,
                nameof(TacGenerator),
                expression.Location,
                "Literal nao suportado no TAC.")
        };
    }

    public string VisitVariable(VariableExpression expression)
    {
        return Variable(expression.Symbol);
    }

    public string VisitUnary(UnaryExpression expression)
    {
        var right = expression.Right.Accept(this);
        var temp = NewTemp();
        var operation = expression.Operator == TokenType.Minus ? "neg" : "not";
        _instructions.Add(new TacInstruction(operation, right, null, temp));
        return temp;
    }

    public string VisitBinary(BinaryExpression expression)
    {
        var left = expression.Left.Accept(this);
        var right = expression.Right.Accept(this);
        var temp = NewTemp();
        _instructions.Add(new TacInstruction(OperationName(expression.Operator), left, right, temp));
        return temp;
    }

    public string VisitGrouping(GroupingExpression expression)
    {
        return expression.Expression.Accept(this);
    }

    private string NewTemp()
    {
        return $"$t{_tempCounter++}";
    }

    private string NewLabel()
    {
        return $"L{_labelCounter++}";
    }

    private static string Variable(SymbolInfo? symbol)
    {
        return symbol is null ? "<sem-simbolo>" : $"v{symbol.Slot}_{symbol.Name}";
    }

    private static string Format(TypeSymbol type)
    {
        return type == TypeSymbol.Int ? "int" : "bool";
    }

    private static string OperationName(TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.Plus => "+",
            TokenType.Minus => "-",
            TokenType.Star => "*",
            TokenType.Slash => "/",
            TokenType.Percent => "%",
            TokenType.EqualEqual => "==",
            TokenType.BangEqual => "!=",
            TokenType.Less => "<",
            TokenType.LessEqual => "<=",
            TokenType.Greater => ">",
            TokenType.GreaterEqual => ">=",
            TokenType.AndAnd => "&&",
            TokenType.OrOr => "||",
            _ => tokenType.ToString()
        };
    }
}
