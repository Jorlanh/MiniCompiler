using MiniCompiler.Bytecode;
using MiniCompiler.Compilation;
using MiniCompiler.Diagnostics;
using MiniCompiler.Input;
using MiniCompiler.Python;
using MiniCompiler.Vm;

namespace MiniCompiler;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            return args.Length == 0
                ? RunInteractive()
                : RunFromArguments(args);
        }
        catch (Exception exception)
        {
            ErrorReporter.Print(exception, "<programa>", string.Empty, Console.Error);
            return 1;
        }
    }

    private static int RunFromArguments(string[] args)
    {
        var options = ParseArguments(args);

        if (options.ContainsKey("--help") || options.ContainsKey("-h"))
        {
            PrintHelp();
            return 0;
        }

        if (options.ContainsKey("--web"))
        {
            return Web.WebFrontend.Run(args);
        }

        var run = options.ContainsKey("--run");
        var showTac = options.ContainsKey("--show-tac");
        var showBytecode = options.ContainsKey("--show-bytecode");
        var sources = LoadSourcesFromOptions(options);

        return CompileMany(sources, run, showTac, showBytecode);
    }

    private static int RunInteractive()
    {
        Console.WriteLine("MiniCompiler - compilador didatico em C#");
        Console.WriteLine("1 - Analisar arquivo");
        Console.WriteLine("2 - Analisar pasta");
        Console.WriteLine("3 - Analisar ZIP");
        Console.WriteLine("4 - Clonar repositorio GitHub e analisar");
        Console.WriteLine("5 - Colar codigo no terminal");
        Console.Write("Opcao: ");

        var option = Console.ReadLine()?.Trim();
        IReadOnlyList<SourceFile> sources;

        switch (option)
        {
            case "1":
                Console.Write("Caminho do arquivo: ");
                sources = ProjectLoader.FromFile(ReadRequiredLine());
                break;
            case "2":
                Console.Write("Caminho da pasta: ");
                sources = ProjectLoader.FromDirectory(ReadRequiredLine());
                break;
            case "3":
                Console.Write("Caminho do ZIP: ");
                sources = ProjectLoader.FromZip(ReadRequiredLine());
                break;
            case "4":
                Console.Write("Link do repositorio GitHub: ");
                sources = ProjectLoader.FromGithub(ReadRequiredLine());
                break;
            case "5":
                sources = new[] { ProjectLoader.FromText(ReadSourceFromConsole()) };
                break;
            default:
                Console.WriteLine("Opcao invalida.");
                return 1;
        }

        Console.Write("Mostrar TAC? (s/n): ");
        var showTac = IsYes(Console.ReadLine());
        Console.Write("Mostrar bytecode? (s/n): ");
        var showBytecode = IsYes(Console.ReadLine());
        Console.Write("Executar depois de compilar? (s/n): ");
        var run = IsYes(Console.ReadLine());

        return CompileMany(sources, run, showTac, showBytecode);
    }

    private static IReadOnlyList<SourceFile> LoadSourcesFromOptions(Dictionary<string, string?> options)
    {
        if (TryGetValue(options, "--file", out var file))
        {
            return ProjectLoader.FromFile(file);
        }

        if (TryGetValue(options, "--dir", out var directory))
        {
            return ProjectLoader.FromDirectory(directory);
        }

        if (TryGetValue(options, "--zip", out var zip))
        {
            return ProjectLoader.FromZip(zip);
        }

        if (TryGetValue(options, "--github", out var github))
        {
            return ProjectLoader.FromGithub(github);
        }

        if (TryGetValue(options, "--source", out var source))
        {
            return new[] { ProjectLoader.FromText(source) };
        }

        throw new CompilerException(
            "Entrada",
            "<argumentos>",
            nameof(Program),
            null,
            "Informe --file, --dir, --zip, --github ou --source. Use --help para ver exemplos.");
    }

    private static int CompileMany(IReadOnlyList<SourceFile> sources, bool run, bool showTac, bool showBytecode)
    {
        var compiler = new MiniCompilerPipeline();
        var pythonCompiler = new PythonCompilerService();
        var success = 0;
        var failed = 0;

        foreach (var source in sources)
        {
            Console.WriteLine();
            Console.WriteLine($"== {source.Name} ==");
            SourceRepairResult? repair = null;

            try
            {
                if (SourceLanguageDetector.IsPython(source.Name, source.Text))
                {
                    var pythonResult = pythonCompiler.Compile(source.Name, source.Text);
                    success++;
                    Console.WriteLine("Compilacao Python concluida.");
                    Console.WriteLine($"Python: {pythonResult.PythonVersion}");
                    Console.WriteLine($"Tokens: {pythonResult.TokenCount}");
                    Console.WriteLine($"Instrucoes TAC: {pythonResult.IntermediateLineCount}");
                    Console.WriteLine($"Instrucoes bytecode: {pythonResult.BytecodeInstructionCount}");
                    Console.WriteLine($"Variaveis: {pythonResult.VariableCount}");
                    Console.WriteLine($"Linhas: {pythonResult.LineCount}");
                    Console.WriteLine($"Nos AST Python: {pythonResult.AstNodeCount}");

                    if (showTac)
                    {
                        Console.WriteLine();
                        Console.WriteLine("TAC / IR Python:");
                        Console.WriteLine(pythonResult.IntermediateCode);
                    }

                    if (showBytecode)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Bytecode Python:");
                        Console.WriteLine(pythonResult.BytecodeText);
                    }

                    if (run)
                    {
                        Console.WriteLine("Execucao Python nao foi iniciada pelo MiniCompiler. O modo Python valida/compila o codigo sem executar input().");
                    }

                    continue;
                }

                repair = SourceAutoCorrector.Repair(source.Name, source.Text);
                var result = compiler.Compile(source.Name, repair.SourceText);
                success++;

                Console.WriteLine("Compilacao concluida.");

                if (repair.HasCorrections)
                {
                    PrintCorrections(repair.Corrections);
                }

                Console.WriteLine($"Tokens: {result.Tokens.Count}");
                Console.WriteLine($"Instrucoes TAC: {result.Tac.Count}");
                Console.WriteLine($"Instrucoes bytecode: {result.Bytecode.Instructions.Count}");
                Console.WriteLine($"Variaveis: {result.Bytecode.VariableTypes.Count}");

                if (showTac)
                {
                    PrintTac(result.Tac);
                }

                if (showBytecode)
                {
                    PrintBytecode(result.Bytecode);
                }

                if (run)
                {
                    Console.WriteLine("Saida do programa:");
                    var vm = new VirtualMachine(source.Name, result.Bytecode, Console.In, Console.Out);
                    vm.Run();
                }
            }
            catch (Exception exception)
            {
                failed++;
                if (repair?.HasCorrections == true)
                {
                    PrintCorrections(repair.Corrections);
                }

                ErrorReporter.Print(exception, source.Name, repair?.SourceText ?? source.Text, Console.Error);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Resumo: {success} arquivo(s) ok, {failed} com erro.");
        return failed == 0 ? 0 : 1;
    }

    private static void PrintTac(IEnumerable<object> instructions)
    {
        Console.WriteLine();
        Console.WriteLine("TAC:");
        foreach (var instruction in instructions)
        {
            Console.WriteLine(instruction);
        }
    }

    private static void PrintBytecode(BytecodeProgram program)
    {
        Console.WriteLine();
        Console.WriteLine("Bytecode:");
        for (var i = 0; i < program.Instructions.Count; i++)
        {
            Console.WriteLine($"{i:D4}: {program.Instructions[i]}");
        }
    }

    private static void PrintCorrections(IReadOnlyList<SourceCorrection> corrections)
    {
        Console.WriteLine("Auto-correcao aplicada:");

        foreach (var correction in corrections)
        {
            Console.WriteLine($"- {correction.Message}");
        }
    }

    private static Dictionary<string, string?> ParseArguments(string[] args)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];

            if (!key.StartsWith("-"))
            {
                throw new CompilerException(
                    "Entrada",
                    "<argumentos>",
                    nameof(Program),
                    null,
                    $"Argumento solto nao reconhecido: {key}");
            }

            string? value = null;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
            {
                value = args[++i];
            }

            options[key] = value;
        }

        return options;
    }

    private static bool TryGetValue(Dictionary<string, string?> options, string key, out string value)
    {
        if (options.TryGetValue(key, out var rawValue) && !string.IsNullOrWhiteSpace(rawValue))
        {
            value = rawValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string ReadSourceFromConsole()
    {
        Console.WriteLine("Cole o codigo. Para terminar, digite uma linha contendo apenas FIM.");
        var lines = new List<string>();

        while (Console.ReadLine() is { } line)
        {
            if (line.Trim().Equals("FIM", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            lines.Add(line);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string ReadRequiredLine()
    {
        var value = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CompilerException("Entrada", "<console>", nameof(Program), null, "Valor obrigatorio nao informado.");
        }

        return value.Trim();
    }

    private static bool IsYes(string? value)
    {
        return value?.Trim().Equals("s", StringComparison.OrdinalIgnoreCase) == true
            || value?.Trim().Equals("sim", StringComparison.OrdinalIgnoreCase) == true
            || value?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true
            || value?.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("MiniCompiler - uso:");
        Console.WriteLine("  dotnet run -- --file caminho/programa.mini --show-tac --show-bytecode --run");
        Console.WriteLine("  dotnet run -- --dir caminho/projeto");
        Console.WriteLine("  dotnet run -- --zip caminho/projeto.zip");
        Console.WriteLine("  dotnet run -- --github https://github.com/usuario/repositorio");
        Console.WriteLine("  dotnet run -- --source \"int x = 2; print(x);\" --run");
        Console.WriteLine("  dotnet run -- --web");
        Console.WriteLine();
        Console.WriteLine("Opcoes:");
        Console.WriteLine("  --show-tac       Mostra codigo de tres enderecos");
        Console.WriteLine("  --show-bytecode  Mostra bytecode da VM");
        Console.WriteLine("  --run            Executa na VM depois de compilar");
        Console.WriteLine("  --web            Abre o frontend web em http://localhost:5055");
    }
}
