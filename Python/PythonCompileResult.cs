namespace MiniCompiler.Python;

public sealed record PythonCompileResult(
    int TokenCount,
    int LineCount,
    int IntermediateLineCount,
    int AstNodeCount,
    int BytecodeInstructionCount,
    int VariableCount,
    string PythonVersion,
    string IntermediateCode,
    string BytecodeText);
