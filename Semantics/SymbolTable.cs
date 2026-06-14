namespace MiniCompiler.Semantics;

public sealed class SymbolTable
{
    private readonly Dictionary<string, SymbolInfo> _symbols = new(StringComparer.Ordinal);

    public SymbolTable(SymbolTable? parent = null)
    {
        Parent = parent;
    }

    public SymbolTable? Parent { get; }

    public bool Declare(SymbolInfo symbol)
    {
        if (_symbols.ContainsKey(symbol.Name))
        {
            return false;
        }

        _symbols.Add(symbol.Name, symbol);
        return true;
    }

    public bool TryResolve(string name, out SymbolInfo? symbol)
    {
        if (_symbols.TryGetValue(name, out symbol))
        {
            return true;
        }

        if (Parent is not null)
        {
            return Parent.TryResolve(name, out symbol);
        }

        symbol = null;
        return false;
    }
}
