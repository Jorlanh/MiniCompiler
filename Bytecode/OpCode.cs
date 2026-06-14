namespace MiniCompiler.Bytecode;

public enum OpCode : byte
{
    PushInt,
    PushBool,
    LoadVar,
    StoreVar,
    Add,
    Sub,
    Mul,
    Div,
    Mod,
    Neg,
    Not,
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    And,
    Or,
    Jump,
    JumpFalse,
    Print,
    ReadInt,
    ReadBool,
    Halt
}
