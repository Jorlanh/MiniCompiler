namespace MiniCompiler.Python;

public sealed record PythonCompileResult(
    int LineCount,
    int AstNodeCount,
    int BytecodeInstructionCount,
    string PythonVersion);
