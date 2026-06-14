using MiniCompiler.Semantics;

namespace MiniCompiler.Ast;

public interface IStatementVisitor<out T>
{
    T VisitVarDeclaration(VarDeclaration statement);
    T VisitAssignment(AssignmentStatement statement);
    T VisitPrint(PrintStatement statement);
    T VisitRead(ReadStatement statement);
    T VisitIf(IfStatement statement);
    T VisitWhile(WhileStatement statement);
    T VisitBlock(BlockStatement statement);
}

public interface IExpressionVisitor<out T>
{
    T VisitLiteral(LiteralExpression expression);
    T VisitVariable(VariableExpression expression);
    T VisitUnary(UnaryExpression expression);
    T VisitBinary(BinaryExpression expression);
    T VisitGrouping(GroupingExpression expression);
}
