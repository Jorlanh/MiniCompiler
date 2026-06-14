using MiniCompiler.Ast;
using MiniCompiler.Diagnostics;
using MiniCompiler.Lexing;

namespace MiniCompiler.Semantics;

public sealed class SemanticAnalyzer : IStatementVisitor<object?>, IExpressionVisitor<TypeSymbol>
{
    private readonly string _sourceName;
    private readonly List<TypeSymbol> _variableTypes = new();
    private SymbolTable _currentScope = new();

    public SemanticAnalyzer(string sourceName)
    {
        _sourceName = sourceName;
    }

    public int VariableCount => _variableTypes.Count;

    public IReadOnlyList<TypeSymbol> VariableTypes => _variableTypes;

    public void Analyze(ProgramNode program)
    {
        try
        {
            foreach (var statement in program.Statements)
            {
                statement.Accept(this);
            }
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CompilerException.Unexpected(
                "Semantico",
                _sourceName,
                nameof(SemanticAnalyzer),
                program.Location,
                exception);
        }
    }

    public object? VisitVarDeclaration(VarDeclaration statement)
    {
        var slot = _variableTypes.Count;
        var symbol = new SymbolInfo(statement.Name, statement.DeclaredType, slot, statement.Location);

        if (!_currentScope.Declare(symbol))
        {
            Fail(statement.Location, $"A variavel '{statement.Name}' ja foi declarada neste escopo.");
        }

        statement.Symbol = symbol;
        _variableTypes.Add(statement.DeclaredType);

        if (statement.Initializer is not null)
        {
            var initializerType = statement.Initializer.Accept(this);
            ExpectSameType(statement.DeclaredType, initializerType, statement.Initializer.Location,
                $"A variavel '{statement.Name}' e do tipo {Format(statement.DeclaredType)}, mas recebeu {Format(initializerType)}.");
        }

        return null;
    }

    public object? VisitAssignment(AssignmentStatement statement)
    {
        var symbol = Resolve(statement.Name, statement.Location);
        statement.Symbol = symbol;

        var valueType = statement.Value.Accept(this);
        ExpectSameType(symbol.Type, valueType, statement.Value.Location,
            $"A variavel '{statement.Name}' e do tipo {Format(symbol.Type)}, mas recebeu {Format(valueType)}.");

        return null;
    }

    public object? VisitPrint(PrintStatement statement)
    {
        statement.Value.Accept(this);
        return null;
    }

    public object? VisitRead(ReadStatement statement)
    {
        statement.Symbol = Resolve(statement.Name, statement.Location);
        return null;
    }

    public object? VisitIf(IfStatement statement)
    {
        var conditionType = statement.Condition.Accept(this);
        ExpectSameType(TypeSymbol.Bool, conditionType, statement.Condition.Location,
            "A condicao do if precisa ser bool.");

        statement.ThenBranch.Accept(this);
        statement.ElseBranch?.Accept(this);
        return null;
    }

    public object? VisitWhile(WhileStatement statement)
    {
        var conditionType = statement.Condition.Accept(this);
        ExpectSameType(TypeSymbol.Bool, conditionType, statement.Condition.Location,
            "A condicao do while precisa ser bool.");

        statement.Body.Accept(this);
        return null;
    }

    public object? VisitBlock(BlockStatement statement)
    {
        var previous = _currentScope;
        _currentScope = new SymbolTable(previous);

        try
        {
            foreach (var childStatement in statement.Statements)
            {
                childStatement.Accept(this);
            }
        }
        finally
        {
            _currentScope = previous;
        }

        return null;
    }

    public TypeSymbol VisitLiteral(LiteralExpression expression)
    {
        var type = expression.Value switch
        {
            int => TypeSymbol.Int,
            bool => TypeSymbol.Bool,
            _ => throw new CompilerException(
                "Semantico",
                _sourceName,
                nameof(SemanticAnalyzer),
                expression.Location,
                $"Literal sem tipo conhecido: {expression.Value}.")
        };

        expression.InferredType = type;
        return type;
    }

    public TypeSymbol VisitVariable(VariableExpression expression)
    {
        var symbol = Resolve(expression.Name, expression.Location);
        expression.Symbol = symbol;
        expression.InferredType = symbol.Type;
        return symbol.Type;
    }

    public TypeSymbol VisitUnary(UnaryExpression expression)
    {
        var rightType = expression.Right.Accept(this);
        TypeSymbol resultType;

        switch (expression.Operator)
        {
            case TokenType.Minus:
                ExpectSameType(TypeSymbol.Int, rightType, expression.Location,
                    "O operador '-' so pode ser usado com int.");
                resultType = TypeSymbol.Int;
                break;
            case TokenType.Bang:
                ExpectSameType(TypeSymbol.Bool, rightType, expression.Location,
                    "O operador '!' so pode ser usado com bool.");
                resultType = TypeSymbol.Bool;
                break;
            default:
                Fail(expression.Location, $"Operador unario invalido: {expression.Operator}.");
                return TypeSymbol.Int;
        }

        expression.InferredType = resultType;
        return resultType;
    }

    public TypeSymbol VisitBinary(BinaryExpression expression)
    {
        var leftType = expression.Left.Accept(this);
        var rightType = expression.Right.Accept(this);

        TypeSymbol resultType;

        switch (expression.Operator)
        {
            case TokenType.Plus:
            case TokenType.Minus:
            case TokenType.Star:
            case TokenType.Slash:
            case TokenType.Percent:
                ExpectSameType(TypeSymbol.Int, leftType, expression.Left.Location,
                    $"O lado esquerdo de '{expression.Operator}' precisa ser int.");
                ExpectSameType(TypeSymbol.Int, rightType, expression.Right.Location,
                    $"O lado direito de '{expression.Operator}' precisa ser int.");
                resultType = TypeSymbol.Int;
                break;
            case TokenType.Less:
            case TokenType.LessEqual:
            case TokenType.Greater:
            case TokenType.GreaterEqual:
                ExpectSameType(TypeSymbol.Int, leftType, expression.Left.Location,
                    "Comparacao numerica precisa receber int dos dois lados.");
                ExpectSameType(TypeSymbol.Int, rightType, expression.Right.Location,
                    "Comparacao numerica precisa receber int dos dois lados.");
                resultType = TypeSymbol.Bool;
                break;
            case TokenType.EqualEqual:
            case TokenType.BangEqual:
                ExpectSameType(leftType, rightType, expression.Right.Location,
                    $"Comparacao de igualdade recebeu {Format(leftType)} e {Format(rightType)}.");
                resultType = TypeSymbol.Bool;
                break;
            case TokenType.AndAnd:
            case TokenType.OrOr:
                ExpectSameType(TypeSymbol.Bool, leftType, expression.Left.Location,
                    "Operador logico precisa receber bool dos dois lados.");
                ExpectSameType(TypeSymbol.Bool, rightType, expression.Right.Location,
                    "Operador logico precisa receber bool dos dois lados.");
                resultType = TypeSymbol.Bool;
                break;
            default:
                Fail(expression.Location, $"Operador binario invalido: {expression.Operator}.");
                return TypeSymbol.Int;
        }

        expression.InferredType = resultType;
        return resultType;
    }

    public TypeSymbol VisitGrouping(GroupingExpression expression)
    {
        var type = expression.Expression.Accept(this);
        expression.InferredType = type;
        return type;
    }

    private SymbolInfo Resolve(string name, SourceLocation location)
    {
        if (_currentScope.TryResolve(name, out var symbol) && symbol is not null)
        {
            return symbol;
        }

        throw new CompilerException(
            "Semantico",
            _sourceName,
            nameof(SemanticAnalyzer),
            location,
            $"A variavel '{name}' nao foi declarada.");
    }

    private void ExpectSameType(TypeSymbol expected, TypeSymbol actual, SourceLocation location, string message)
    {
        if (expected != actual)
        {
            throw new CompilerException(
                "Semantico",
                _sourceName,
                nameof(SemanticAnalyzer),
                location,
                message);
        }
    }

    private void Fail(SourceLocation location, string message)
    {
        throw new CompilerException(
            "Semantico",
            _sourceName,
            nameof(SemanticAnalyzer),
            location,
            message);
    }

    private static string Format(TypeSymbol type)
    {
        return type == TypeSymbol.Int ? "int" : "bool";
    }
}
