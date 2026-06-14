using MiniCompiler.Diagnostics;

namespace MiniCompiler.Semantics;

public sealed record SymbolInfo(string Name, TypeSymbol Type, int Slot, SourceLocation DeclaredAt);
