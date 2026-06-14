namespace MiniCompiler.Bytecode;

public enum OpCode
{
    PushInt,
    PushBool,
    PushString,
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
    PrintInline,
    ReadInt,
    ReadBool,
    Halt
}