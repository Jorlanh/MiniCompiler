using MiniCompiler.Ast;
using MiniCompiler.Diagnostics;
using MiniCompiler.Lexing;
using MiniCompiler.Semantics;

namespace MiniCompiler.Bytecode;

public sealed class BytecodeEmitter : IStatementVisitor<object?>, IExpressionVisitor<object?>
{
    private readonly string _sourceName;
    private readonly IReadOnlyList<TypeSymbol> _variableTypes;
    private readonly List<Instruction> _instructions = new();

    public BytecodeEmitter(string sourceName, IReadOnlyList<TypeSymbol> variableTypes)
    {
        _sourceName = sourceName;
        _variableTypes = variableTypes;
    }

    public BytecodeProgram Emit(ProgramNode program)
    {
        try
        {
            foreach (var statement in program.Statements)
            {
                statement.Accept(this);
            }

            Emit(OpCode.Halt, null, program.Location);
            return new BytecodeProgram(_instructions, _variableTypes.ToArray());
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CompilerException.Unexpected(
                "Bytecode",
                _sourceName,
                nameof(BytecodeEmitter),
                program.Location,
                exception);
        }
    }

    public object? VisitVarDeclaration(VarDeclaration statement)
    {
        if (statement.Initializer is not null)
        {
            statement.Initializer.Accept(this);
        }
        else
        {
            if (statement.DeclaredType == TypeSymbol.Int)
                Emit(OpCode.PushInt, 0, statement.Location);
            else
                Emit(OpCode.PushBool, false, statement.Location);
        }

        var name = statement.Symbol?.Name ?? throw MissingSymbol(statement.Location);
        Emit(OpCode.StoreVar, name, statement.Location);
        return null;
    }

    public object? VisitAssignment(AssignmentStatement statement)
    {
        statement.Value.Accept(this);
        var name = statement.Symbol?.Name ?? throw MissingSymbol(statement.Location);
        Emit(OpCode.StoreVar, name, statement.Location);
        return null;
    }

    public object? VisitPrint(PrintStatement statement)
    {
        statement.Value.Accept(this);
        Emit(OpCode.Print, null, statement.Location);
        return null;
    }

    public object? VisitRead(ReadStatement statement)
    {
        var symbol = statement.Symbol ?? throw MissingSymbol(statement.Location);
        Emit(symbol.Type == TypeSymbol.Int ? OpCode.ReadInt : OpCode.ReadBool, null, statement.Location);
        Emit(OpCode.StoreVar, symbol.Name, statement.Location);
        return null;
    }

    public object? VisitIf(IfStatement statement)
    {
        statement.Condition.Accept(this);
        var jumpFalse = Emit(OpCode.JumpFalse, -1, statement.Location);

        statement.ThenBranch.Accept(this);

        if (statement.ElseBranch is not null)
        {
            var jumpEnd = Emit(OpCode.Jump, -1, statement.Location);
            Patch(jumpFalse, _instructions.Count);
            statement.ElseBranch.Accept(this);
            Patch(jumpEnd, _instructions.Count);
        }
        else
        {
            Patch(jumpFalse, _instructions.Count);
        }

        return null;
    }

    public object? VisitWhile(WhileStatement statement)
    {
        var start = _instructions.Count;
        statement.Condition.Accept(this);
        var jumpFalse = Emit(OpCode.JumpFalse, -1, statement.Location);
        statement.Body.Accept(this);
        Emit(OpCode.Jump, start, statement.Location);
        Patch(jumpFalse, _instructions.Count);
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

    public object? VisitLiteral(LiteralExpression expression)
    {
        switch (expression.Value)
        {
            case int value:
                Emit(OpCode.PushInt, value, expression.Location);
                break;
            case bool value:
                Emit(OpCode.PushBool, value, expression.Location);
                break;
            default:
                throw new CompilerException(
                    "Bytecode",
                    _sourceName,
                    nameof(BytecodeEmitter),
                    expression.Location,
                    "Literal nao suportado no bytecode.");
        }

        return null;
    }

    public object? VisitVariable(VariableExpression expression)
    {
        var name = expression.Symbol?.Name ?? throw MissingSymbol(expression.Location);
        Emit(OpCode.LoadVar, name, expression.Location);
        return null;
    }

    public object? VisitUnary(UnaryExpression expression)
    {
        expression.Right.Accept(this);

        switch (expression.Operator)
        {
            case TokenType.Minus:
                Emit(OpCode.Neg, null, expression.Location);
                break;
            case TokenType.Bang:
                Emit(OpCode.Not, null, expression.Location);
                break;
            default:
                throw new CompilerException(
                    "Bytecode",
                    _sourceName,
                    nameof(BytecodeEmitter),
                    expression.Location,
                    $"Operador unario nao suportado: {expression.Operator}.");
        }

        return null;
    }

    public object? VisitBinary(BinaryExpression expression)
    {
        expression.Left.Accept(this);
        expression.Right.Accept(this);

        Emit(expression.Operator switch
        {
            TokenType.Plus => OpCode.Add,
            TokenType.Minus => OpCode.Sub,
            TokenType.Star => OpCode.Mul,
            TokenType.Slash => OpCode.Div,
            TokenType.Percent => OpCode.Mod,
            TokenType.EqualEqual => OpCode.Equal,
            TokenType.BangEqual => OpCode.NotEqual,
            TokenType.Less => OpCode.Less,
            TokenType.LessEqual => OpCode.LessEqual,
            TokenType.Greater => OpCode.Greater,
            TokenType.GreaterEqual => OpCode.GreaterEqual,
            TokenType.AndAnd => OpCode.And,
            TokenType.OrOr => OpCode.Or,
            _ => throw new CompilerException(
                "Bytecode",
                _sourceName,
                nameof(BytecodeEmitter),
                expression.Location,
                $"Operador binario nao suportado: {expression.Operator}.")
        }, null, expression.Location);

        return null;
    }

    public object? VisitGrouping(GroupingExpression expression)
    {
        expression.Expression.Accept(this);
        return null;
    }

    private int Emit(OpCode code, object? operand, SourceLocation location)
    {
        _instructions.Add(new Instruction(code, operand, location));
        return _instructions.Count - 1;
    }

    private void Patch(int instructionIndex, int targetAddress)
    {
        _instructions[instructionIndex] = _instructions[instructionIndex] with { Operand = targetAddress };
    }

    private CompilerException MissingSymbol(SourceLocation location)
    {
        return new CompilerException(
            "Bytecode",
            _sourceName,
            nameof(BytecodeEmitter),
            location,
            "Simbolo ausente. A analise semantica deveria ter preenchido isso antes do bytecode.");
    }
}