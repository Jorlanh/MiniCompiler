using MiniCompiler.Bytecode;
using MiniCompiler.Diagnostics;
using MiniCompiler.Semantics;

namespace MiniCompiler.Vm;

public sealed class VirtualMachine
{
    private readonly string _sourceName;
    private readonly BytecodeProgram _program;
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly Stack<object> _stack = new();
    private int _pc;

    private static readonly Dictionary<string, object> _globalMemory = new(StringComparer.Ordinal);

    public VirtualMachine(string sourceName, BytecodeProgram program, TextReader input, TextWriter output)
    {
        _sourceName = sourceName;
        _program = program;
        _input = input;
        _output = output;
    }

    public void Run()
    {
        try
        {
            while (_pc < _program.Instructions.Count)
            {
                var instruction = _program.Instructions[_pc];
                _pc++;

                switch (instruction.Code)
                {
                    case OpCode.PushInt:
                        _stack.Push((int)instruction.Operand!);
                        break;
                    case OpCode.PushBool:
                        _stack.Push((bool)instruction.Operand!);
                        break;
                    case OpCode.LoadVar:
                        var loadName = (string)instruction.Operand!;
                        if (!_globalMemory.TryGetValue(loadName, out var storedValue))
                        {
                            RuntimeFail(instruction, $"A variavel '{loadName}' nao foi inicializada na memoria global.");
                        }
                        _stack.Push(storedValue);
                        break;
                    case OpCode.StoreVar:
                        var storeName = (string)instruction.Operand!;
                        _globalMemory[storeName] = Pop(instruction);
                        break;
                    case OpCode.Add:
                        BinaryInt(instruction, (a, b) => a + b);
                        break;
                    case OpCode.Sub:
                        BinaryInt(instruction, (a, b) => a - b);
                        break;
                    case OpCode.Mul:
                        BinaryInt(instruction, (a, b) => a * b);
                        break;
                    case OpCode.Div:
                        BinaryInt(instruction, (a, b) =>
                        {
                            if (b == 0)
                            {
                                RuntimeFail(instruction, "Divisao por zero.");
                            }

                            return a / b;
                        });
                        break;
                    case OpCode.Mod:
                        BinaryInt(instruction, (a, b) =>
                        {
                            if (b == 0)
                            {
                                RuntimeFail(instruction, "Modulo por zero.");
                            }

                            return a % b;
                        });
                        break;
                    case OpCode.Neg:
                        _stack.Push(-PopInt(instruction));
                        break;
                    case OpCode.Not:
                        _stack.Push(!PopBool(instruction));
                        break;
                    case OpCode.Equal:
                        BinaryAny(instruction, (a, b) => Equals(a, b));
                        break;
                    case OpCode.NotEqual:
                        BinaryAny(instruction, (a, b) => !Equals(a, b));
                        break;
                    case OpCode.Less:
                        CompareInt(instruction, (a, b) => a < b);
                        break;
                    case OpCode.LessEqual:
                        CompareInt(instruction, (a, b) => a <= b);
                        break;
                    case OpCode.Greater:
                        CompareInt(instruction, (a, b) => a > b);
                        break;
                    case OpCode.GreaterEqual:
                        CompareInt(instruction, (a, b) => a >= b);
                        break;
                    case OpCode.And:
                        BinaryBool(instruction, (a, b) => a && b);
                        break;
                    case OpCode.Or:
                        BinaryBool(instruction, (a, b) => a || b);
                        break;
                    case OpCode.Jump:
                        _pc = (int)instruction.Operand!;
                        break;
                    case OpCode.JumpFalse:
                        var condition = PopBool(instruction);
                        if (!condition)
                        {
                            _pc = (int)instruction.Operand!;
                        }
                        break;
                    case OpCode.Print:
                        PrintValue(Pop(instruction));
                        break;
                    case OpCode.ReadInt:
                        _stack.Push(ReadInt(instruction));
                        break;
                    case OpCode.ReadBool:
                        _stack.Push(ReadBool(instruction));
                        break;
                    case OpCode.Halt:
                        return;
                    default:
                        RuntimeFail(instruction, $"Opcode desconhecido: {instruction.Code}.");
                        break;
                }
            }
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (Exception exception)
        {
            SourceLocation? location = _pc > 0 && _pc - 1 < _program.Instructions.Count
                ? _program.Instructions[_pc - 1].Location
                : null;

            throw CompilerException.Unexpected(
                "Execucao",
                _sourceName,
                nameof(VirtualMachine),
                location,
                exception);
        }
    }

    private object Pop(Instruction instruction)
    {
        if (_stack.Count == 0)
        {
            RuntimeFail(instruction, "A pilha da VM esta vazia.");
        }

        return _stack.Pop();
    }

    private int PopInt(Instruction instruction)
    {
        var value = Pop(instruction);
        if (value is int intValue)
        {
            return intValue;
        }

        RuntimeFail(instruction, $"Esperava int na pilha, mas recebeu {value.GetType().Name}.");
        return 0;
    }

    private bool PopBool(Instruction instruction)
    {
        var value = Pop(instruction);
        if (value is bool boolValue)
        {
            return boolValue;
        }

        RuntimeFail(instruction, $"Esperava bool na pilha, mas recebeu {value.GetType().Name}.");
        return false;
    }

    private void BinaryInt(Instruction instruction, Func<int, int, int> operation)
    {
        var right = PopInt(instruction);
        var left = PopInt(instruction);
        _stack.Push(operation(left, right));
    }

    private void CompareInt(Instruction instruction, Func<int, int, bool> operation)
    {
        var right = PopInt(instruction);
        var left = PopInt(instruction);
        _stack.Push(operation(left, right));
    }

    private void BinaryBool(Instruction instruction, Func<bool, bool, bool> operation)
    {
        var right = PopBool(instruction);
        var left = PopBool(instruction);
        _stack.Push(operation(left, right));
    }

    private void BinaryAny(Instruction instruction, Func<object, object, bool> operation)
    {
        var right = Pop(instruction);
        var left = Pop(instruction);
        _stack.Push(operation(left, right));
    }

    private int ReadInt(Instruction instruction)
    {
        _output.Write("int> ");
        var text = _input.ReadLine();

        if (!int.TryParse(text, out var value))
        {
            RuntimeFail(instruction, $"Entrada invalida para int: '{text}'.");
        }

        return value;
    }

    private bool ReadBool(Instruction instruction)
    {
        _output.Write("bool> ");
        var text = _input.ReadLine()?.Trim().ToLowerInvariant();

        return text switch
        {
            "true" or "1" => true,
            "false" or "0" => false,
            _ => throw new CompilerException(
                "Execucao",
                _sourceName,
                nameof(VirtualMachine),
                instruction.Location,
                $"Entrada invalida para bool: '{text}'. Use true/false ou 1/0.")
        };
    }

    private void PrintValue(object value)
    {
        _output.WriteLine(value is bool boolValue ? boolValue.ToString().ToLowerInvariant() : value);
    }

    private void RuntimeFail(Instruction instruction, string message)
    {
        throw new CompilerException(
            "Execucao",
            _sourceName,
            nameof(VirtualMachine),
            instruction.Location,
            message);
    }
}