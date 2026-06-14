using MiniCompiler.Diagnostics;
using MiniCompiler.Lexing;
using MiniCompiler.Semantics;

namespace MiniCompiler.Ast;

public abstract record AstNode(SourceLocation Location);

public sealed record ProgramNode(IReadOnlyList<Statement> Statements, SourceLocation Location)
    : AstNode(Location);

public abstract record Statement(SourceLocation Location) : AstNode(Location)
{
    public abstract T Accept<T>(IStatementVisitor<T> visitor);
}

public abstract record Expression(SourceLocation Location) : AstNode(Location)
{
    public TypeSymbol? InferredType { get; set; }
    public abstract T Accept<T>(IExpressionVisitor<T> visitor);
}

public sealed record VarDeclaration(
    TypeSymbol DeclaredType,
    string Name,
    Expression? Initializer,
    SourceLocation Location) : Statement(Location)
{
    public SymbolInfo? Symbol { get; set; }

    public override T Accept<T>(IStatementVisitor<T> visitor)
    {
        return visitor.VisitVarDeclaration(this);
    }
}

public sealed record AssignmentStatement(
    string Name,
    Expression Value,
    SourceLocation Location) : Statement(Location)
{
    public SymbolInfo? Symbol { get; set; }

    public override T Accept<T>(IStatementVisitor<T> visitor)
    {
        return visitor.VisitAssignment(this);
    }
}

public sealed record PrintStatement(Expression Value, SourceLocation Location) : Statement(Location)
{
    public override T Accept<T>(IStatementVisitor<T> visitor)
    {
        return visitor.VisitPrint(this);
    }
}

public sealed record ReadStatement(string Name, SourceLocation Location) : Statement(Location)
{
    public SymbolInfo? Symbol { get; set; }

    public override T Accept<T>(IStatementVisitor<T> visitor)
    {
        return visitor.VisitRead(this);
    }
}

public sealed record IfStatement(
    Expression Condition,
    Statement ThenBranch,
    Statement? ElseBranch,
    SourceLocation Location) : Statement(Location)
{
    public override T Accept<T>(IStatementVisitor<T> visitor)
    {
        return visitor.VisitIf(this);
    }
}

public sealed record WhileStatement(
    Expression Condition,
    Statement Body,
    SourceLocation Location) : Statement(Location)
{
    public override T Accept<T>(IStatementVisitor<T> visitor)
    {
        return visitor.VisitWhile(this);
    }
}

public sealed record BlockStatement(IReadOnlyList<Statement> Statements, SourceLocation Location) : Statement(Location)
{
    public override T Accept<T>(IStatementVisitor<T> visitor)
    {
        return visitor.VisitBlock(this);
    }
}

public sealed record LiteralExpression(object Value, SourceLocation Location) : Expression(Location)
{
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.VisitLiteral(this);
    }
}

public sealed record VariableExpression(string Name, SourceLocation Location) : Expression(Location)
{
    public SymbolInfo? Symbol { get; set; }

    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.VisitVariable(this);
    }
}

public sealed record UnaryExpression(TokenType Operator, Expression Right, SourceLocation Location)
    : Expression(Location)
{
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.VisitUnary(this);
    }
}

public sealed record BinaryExpression(Expression Left, TokenType Operator, Expression Right, SourceLocation Location)
    : Expression(Location)
{
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.VisitBinary(this);
    }
}

public sealed record GroupingExpression(Expression Expression, SourceLocation Location)
    : Expression(Location)
{
    public override T Accept<T>(IExpressionVisitor<T> visitor)
    {
        return visitor.VisitGrouping(this);
    }
}
