using System.Diagnostics;
using System.IO.Compression;
using MiniCompiler.Diagnostics;

namespace MiniCompiler.Input;

public static class ProjectLoader
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mini",
        ".mc",
        ".mcomp",
        ".txt",
        ".py"
    };

    public static SourceFile FromText(string sourceText)
    {
        return new SourceFile("<codigo digitado>", sourceText);
    }

    public static IReadOnlyList<SourceFile> FromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Fail(filePath, $"Arquivo nao encontrado: {filePath}");
            }

            return new[] { new SourceFile(Path.GetFullPath(filePath), File.ReadAllText(filePath)) };
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CompilerException.Unexpected("Entrada", filePath, nameof(ProjectLoader), null, exception);
        }
    }

    public static IReadOnlyList<SourceFile> FromDirectory(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                Fail(directoryPath, $"Pasta nao encontrada: {directoryPath}");
            }

            return LoadSourceFiles(Path.GetFullPath(directoryPath), directoryPath);
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CompilerException.Unexpected("Entrada", directoryPath, nameof(ProjectLoader), null, exception);
        }
    }

    public static IReadOnlyList<SourceFile> FromZip(string zipPath)
    {
        try
        {
            if (!File.Exists(zipPath))
            {
                Fail(zipPath, $"Arquivo ZIP nao encontrado: {zipPath}");
            }

            var tempDirectory = CreateTempDirectory("minicompiler_zip_");
            ZipFile.ExtractToDirectory(zipPath, tempDirectory);
            return LoadSourceFiles(tempDirectory, zipPath);
        }
        catch (InvalidDataException exception)
        {
            throw new CompilerException(
                "Entrada",
                zipPath,
                nameof(ProjectLoader),
                null,
                $"ZIP invalido ou corrompido: {exception.Message}",
                exception);
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CompilerException.Unexpected("Entrada", zipPath, nameof(ProjectLoader), null, exception);
        }
    }

    public static IReadOnlyList<SourceFile> FromGithub(string repositoryUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(repositoryUrl))
            {
                Fail("GitHub", "Informe o link do repositorio GitHub.");
            }

            if (!repositoryUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
                && !repositoryUrl.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
            {
                Fail(repositoryUrl, "Por seguranca, esta entrada aceita links http/https do github.com.");
            }

            var tempDirectory = CreateTempDirectory("minicompiler_git_");
            CloneRepository(repositoryUrl, tempDirectory);
            return LoadSourceFiles(tempDirectory, repositoryUrl);
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw CompilerException.Unexpected("Entrada", repositoryUrl, nameof(ProjectLoader), null, exception);
        }
    }

    private static IReadOnlyList<SourceFile> LoadSourceFiles(string rootDirectory, string sourceName)
    {
        var files = Directory
            .EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .Select(file => new SourceFile(file, File.ReadAllText(file)))
            .ToList();

        if (files.Count == 0)
        {
            Fail(sourceName, "Nenhum arquivo de codigo foi encontrado. Extensoes aceitas: .mini, .mc, .mcomp, .txt e .py.");
        }

        return files;
    }

    private static bool IsSourceFile(string filePath)
    {
        if (filePath.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return SupportedExtensions.Contains(Path.GetExtension(filePath));
    }

    private static void CloneRepository(string repositoryUrl, string targetDirectory)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("clone");
        startInfo.ArgumentList.Add("--depth");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add(repositoryUrl);
        startInfo.ArgumentList.Add(targetDirectory);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new CompilerException(
                "Entrada",
                repositoryUrl,
                nameof(ProjectLoader),
                null,
                "Nao foi possivel iniciar o git clone.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(error) ? output : error;
            throw new CompilerException(
                "Entrada",
                repositoryUrl,
                nameof(ProjectLoader),
                null,
                $"Falha ao clonar o repositorio: {message.Trim()}");
        }
    }

    private static string CreateTempDirectory(string prefix)
    {
        var directory = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void Fail(string sourceName, string message)
    {
        throw new CompilerException("Entrada", sourceName, nameof(ProjectLoader), null, message);
    }
}
