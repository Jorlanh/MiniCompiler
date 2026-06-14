using System.Net;
using System.Text;
using MiniCompiler.Bytecode;
using MiniCompiler.Compilation;
using MiniCompiler.Diagnostics;
using MiniCompiler.Input;
using MiniCompiler.Python;

namespace MiniCompiler.Web;

public static class WebFrontend
{
    private const string DefaultUrl = "http://localhost:5055";

    public static int Run(string[] args)
    {
        var url = ReadUrl(args);
        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.UseUrls(url);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
        });

        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception exception)
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(RenderPage(RenderException(exception, "Aplicacao web", string.Empty)));
            }
        });

        app.MapGet("/", () => Html(RenderPage()));
        app.MapPost("/analisar/codigo", (Func<HttpContext, Task<IResult>>)AnalyzeCode);
        app.MapPost("/analisar/github", (Func<HttpContext, Task<IResult>>)AnalyzeGithub);
        app.MapPost("/analisar/zip", (Func<HttpContext, Task<IResult>>)AnalyzeZip);

        Console.WriteLine($"Frontend rodando em {url}");
        app.Run();
        return 0;
    }

    private static async Task<IResult> AnalyzeCode(HttpContext context)
    {
        try
        {
            var form = await context.Request.ReadFormAsync();
            var code = form["source"].ToString();

            if (string.IsNullOrWhiteSpace(code))
            {
                return Html(RenderPage(RenderError("Codigo", "Informe um codigo para compilar."), "codigo"));
            }

            var sources = new[] { ProjectLoader.FromText(code) };
            return Html(RenderPage(BuildReport(sources), "codigo"));
        }
        catch (Exception exception)
        {
            return Html(RenderPage(RenderException(exception, "Codigo", string.Empty), "codigo"));
        }
    }

    private static async Task<IResult> AnalyzeGithub(HttpContext context)
    {
        try
        {
            var form = await context.Request.ReadFormAsync();
            var url = form["githubUrl"].ToString();

            if (string.IsNullOrWhiteSpace(url))
            {
                return Html(RenderPage(RenderError("GitHub", "Informe o link do repositorio."), "github"));
            }

            return Html(RenderPage(BuildReport(ProjectLoader.FromGithub(url)), "github"));
        }
        catch (Exception exception)
        {
            return Html(RenderPage(RenderException(exception, "Repositorio GitHub", string.Empty), "github"));
        }
    }

    private static async Task<IResult> AnalyzeZip(HttpContext context)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"minicompiler_upload_{Guid.NewGuid():N}.zip");
        var fileName = "Arquivo ZIP";

        try
        {
            var form = await context.Request.ReadFormAsync();
            var file = form.Files["zipFile"];

            if (file is null || file.Length == 0)
            {
                return Html(RenderPage(RenderError("ZIP", "Selecione um arquivo .zip."), "zip"));
            }

            fileName = file.FileName;

            await using (var stream = File.Create(tempPath))
            {
                await file.CopyToAsync(stream);
            }

            return Html(RenderPage(BuildReport(ProjectLoader.FromZip(tempPath)), "zip"));
        }
        catch (Exception exception)
        {
            return Html(RenderPage(RenderException(exception, fileName, string.Empty), "zip"));
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static string BuildReport(IReadOnlyList<SourceFile> sources)
    {
        var compiler = new MiniCompilerPipeline();
        var pythonCompiler = new PythonCompilerService();
        var builder = new StringBuilder();
        var success = 0;
        var failed = 0;

        builder.AppendLine("<section class=\"result-list\" aria-live=\"polite\">");

        foreach (var source in sources)
        {
            SourceRepairResult? repair = null;

            try
            {
                if (SourceLanguageDetector.IsPython(source.Name, source.Text))
                {
                    var pythonResult = pythonCompiler.Compile(source.Name, source.Text);
                    success++;
                    builder.AppendLine(RenderPythonSuccess(source.Name, pythonResult));
                    continue;
                }

                repair = SourceAutoCorrector.Repair(source.Name, source.Text);
                var result = compiler.Compile(source.Name, repair.SourceText);
                success++;

                builder.AppendLine("<article class=\"result ok\">");
                builder.AppendLine($"<h2>{Escape(DisplayName(source.Name))}</h2>");

                if (repair.HasCorrections)
                {
                    builder.AppendLine(RenderCorrections(repair.Corrections));
                }

                builder.AppendLine("<div class=\"metrics\">");
                builder.AppendLine(Metric("Tokens", result.Tokens.Count));
                builder.AppendLine(Metric("TAC", result.Tac.Count));
                builder.AppendLine(Metric("Bytecode", result.Bytecode.Instructions.Count));
                builder.AppendLine(Metric("Variaveis", result.Bytecode.VariableTypes.Count));
                builder.AppendLine("</div>");
                builder.AppendLine("<details open><summary>TAC</summary>");
                builder.AppendLine($"<pre>{Escape(string.Join(Environment.NewLine, result.Tac))}</pre>");
                builder.AppendLine("</details>");
                builder.AppendLine("<details><summary>Bytecode</summary>");
                builder.AppendLine($"<pre>{Escape(FormatBytecode(result.Bytecode.Instructions))}</pre>");
                builder.AppendLine("</details>");
                builder.AppendLine("</article>");
            }
            catch (Exception exception)
            {
                failed++;
                if (repair?.HasCorrections == true)
                {
                    builder.AppendLine(RenderCorrections(repair.Corrections));
                }

                var sourceText = exception is CompilerException ? repair?.SourceText ?? source.Text : string.Empty;
                builder.AppendLine(RenderException(exception, source.Name, sourceText));
            }
        }

        builder.AppendLine("</section>");
        builder.Insert(0, $"<div class=\"summary\"><strong>{success}</strong> arquivo(s) ok <span>{failed} com erro</span></div>");

        return builder.ToString();
    }

    private static string RenderPythonSuccess(string sourceName, PythonCompileResult result)
    {
        var builder = new StringBuilder();

        builder.AppendLine("<article class=\"result ok\">");
        builder.AppendLine("<div class=\"result-heading\">");
        builder.AppendLine("<div>");
        builder.AppendLine("<span class=\"eyebrow\">Python puro</span>");
        builder.AppendLine($"<h2>{Escape(DisplayName(sourceName))}</h2>");
        builder.AppendLine("</div>");
        builder.AppendLine("<strong>Python</strong>");
        builder.AppendLine("</div>");
        builder.AppendLine("<p class=\"message\">Codigo Python compilado com sucesso pelo backend CPython.</p>");
        builder.AppendLine("<div class=\"metrics\">");
        builder.AppendLine(Metric("Python", result.PythonVersion));
        builder.AppendLine(Metric("Linhas", result.LineCount));
        builder.AppendLine(Metric("Nos AST", result.AstNodeCount));
        builder.AppendLine(Metric("Bytecode py", result.BytecodeInstructionCount));
        builder.AppendLine("</div>");
        builder.AppendLine("</article>");

        return builder.ToString();
    }

    private static string RenderException(Exception exception, string sourceName, string sourceText)
    {
        var diagnostic = exception is CompilerException compilerException
            ? ErrorReporter.BuildDiagnostic(compilerException, sourceText)
            : ErrorReporter.BuildDiagnostic(CompilerException.Unexpected("Web", sourceName, nameof(WebFrontend), null, exception), sourceText);

        var position = diagnostic.Location is { } location
            ? $"linha {location.Line}, coluna {location.Column}"
            : "entrada do projeto";

        var snippet = !string.IsNullOrWhiteSpace(diagnostic.LineText)
            ? $"""
                <div class="snippet" aria-label="Trecho com erro">
                    <code>{Escape(diagnostic.LineText)}</code>
                    <code class="caret">{Escape(diagnostic.Caret)}</code>
                </div>
              """
            : "<p class=\"empty-detail\">Nao existe uma linha de codigo para marcar neste erro. O problema aconteceu antes de carregar os arquivos.</p>";

        return $"""
        <article class="result error">
            <div class="result-heading">
                <div>
                    <span class="eyebrow">Erro tratado</span>
                    <h2>{Escape(DisplayName(sourceName))}</h2>
                </div>
                <strong>{Escape(diagnostic.Stage)}</strong>
            </div>
            <p class="message">{Escape(diagnostic.Message)}</p>
            <div class="diagnostic-grid">
                <div><span>Arquivo</span><strong>{Escape(diagnostic.SourceName)}</strong></div>
                <div><span>Classe</span><strong>{Escape(diagnostic.ClassName)}</strong></div>
                <div><span>Posicao</span><strong>{Escape(position)}</strong></div>
            </div>
            {snippet}
        </article>
        """;
    }

    private static string RenderCorrections(IReadOnlyList<SourceCorrection> corrections)
    {
        if (corrections.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        builder.AppendLine("<div class=\"corrections\">");
        builder.AppendLine("<div class=\"corrections-title\">Auto-correcao aplicada</div>");

        foreach (var correction in corrections)
        {
            builder.AppendLine("<div class=\"correction-item\">");
            builder.AppendLine($"<strong>{Escape(correction.Message)}</strong>");
            builder.AppendLine($"<span>linha {correction.Location.Line}, coluna {correction.Location.Column}</span>");

            if (!string.IsNullOrWhiteSpace(correction.OriginalLine))
            {
                builder.AppendLine("<pre>");
                builder.AppendLine(Escape("- " + correction.OriginalLine));
                builder.AppendLine(Escape("+ " + correction.CorrectedLine));
                builder.AppendLine("</pre>");
            }

            builder.AppendLine("</div>");
        }

        builder.AppendLine("</div>");
        return builder.ToString();
    }

    private static string RenderError(string title, string message)
    {
        return $"""
        <article class="result error">
            <div class="result-heading">
                <div>
                    <span class="eyebrow">Entrada invalida</span>
                    <h2>{Escape(title)}</h2>
                </div>
            </div>
            <p class="message">{Escape(message)}</p>
        </article>
        """;
    }

    private static string RenderPage(string resultHtml = "", string activeTab = "codigo")
    {
        var codeActive = activeTab == "codigo" ? "active" : "";
        var githubActive = activeTab == "github" ? "active" : "";
        var zipActive = activeTab == "zip" ? "active" : "";
        var sampleCode = Escape("""
            int n = 5;
            int fat = 1;

            while (n > 1) {
                fat = fat * n;
                n = n - 1;
            }

            print(fat);
            """);

        return $$"""
        <!doctype html>
        <html lang="pt-BR">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>MiniCompiler</title>
            <style>
                :root {
                    --bg: #f5f7f8;
                    --panel: #ffffff;
                    --ink: #1d2527;
                    --muted: #697579;
                    --line: #d8e0e3;
                    --accent: #087f8c;
                    --accent-dark: #065d67;
                    --error: #b3261e;
                    --ok: #276749;
                    --code: #132326;
                }

                * {
                    box-sizing: border-box;
                }

                body {
                    margin: 0;
                    font-family: Arial, Helvetica, sans-serif;
                    background: var(--bg);
                    color: var(--ink);
                }

                header {
                    border-bottom: 1px solid var(--line);
                    background: #ffffff;
                }

                .wrap {
                    width: min(1180px, calc(100% - 32px));
                    margin: 0 auto;
                }

                .topbar {
                    min-height: 72px;
                    display: flex;
                    align-items: center;
                    justify-content: space-between;
                    gap: 16px;
                }

                h1 {
                    margin: 0;
                    font-size: 24px;
                    font-weight: 700;
                    letter-spacing: 0;
                }

                .status {
                    color: var(--muted);
                    font-size: 14px;
                }

                main {
                    padding: 24px 0 40px;
                }

                .workspace {
                    display: grid;
                    grid-template-columns: 380px minmax(0, 1fr);
                    gap: 20px;
                    align-items: start;
                }

                .panel,
                .result,
                .summary {
                    background: var(--panel);
                    border: 1px solid var(--line);
                    border-radius: 8px;
                }

                .panel {
                    overflow: hidden;
                }

                .tabs {
                    display: grid;
                    grid-template-columns: repeat(3, 1fr);
                    border-bottom: 1px solid var(--line);
                    background: #edf3f4;
                }

                .tab {
                    min-height: 44px;
                    border: 0;
                    border-right: 1px solid var(--line);
                    background: transparent;
                    color: var(--ink);
                    cursor: pointer;
                    font-weight: 700;
                    font-size: 14px;
                }

                .tab:last-child {
                    border-right: 0;
                }

                .tab.active {
                    background: #ffffff;
                    color: var(--accent-dark);
                    box-shadow: inset 0 -3px 0 var(--accent);
                }

                .pane {
                    display: none;
                    padding: 18px;
                }

                .pane.active {
                    display: block;
                }

                label {
                    display: block;
                    margin-bottom: 8px;
                    font-size: 14px;
                    font-weight: 700;
                }

                textarea,
                input[type="url"],
                input[type="file"] {
                    width: 100%;
                    border: 1px solid var(--line);
                    border-radius: 6px;
                    background: #fbfdfd;
                    color: var(--ink);
                    font: 14px Consolas, "Courier New", monospace;
                }

                textarea {
                    min-height: 280px;
                    resize: vertical;
                    padding: 12px;
                    line-height: 1.45;
                }

                input[type="url"],
                input[type="file"] {
                    min-height: 44px;
                    padding: 10px;
                }

                .actions {
                    margin-top: 14px;
                    display: flex;
                    justify-content: flex-end;
                }

                button[type="submit"] {
                    min-height: 42px;
                    border: 0;
                    border-radius: 6px;
                    padding: 0 18px;
                    background: var(--accent);
                    color: #ffffff;
                    cursor: pointer;
                    font-weight: 700;
                    font-size: 14px;
                }

                button[type="submit"]:hover {
                    background: var(--accent-dark);
                }

                .summary {
                    padding: 14px 16px;
                    margin-bottom: 14px;
                    display: flex;
                    gap: 12px;
                    align-items: center;
                    color: var(--muted);
                }

                .summary strong {
                    color: var(--ok);
                    font-size: 22px;
                }

                .result-list {
                    display: grid;
                    gap: 14px;
                }

                .result {
                    padding: 16px;
                }

                .result h2 {
                    margin: 0 0 12px;
                    font-size: 17px;
                    overflow-wrap: anywhere;
                }

                .result-heading {
                    display: flex;
                    justify-content: space-between;
                    gap: 12px;
                    align-items: start;
                    margin-bottom: 10px;
                }

                .result-heading h2 {
                    margin-bottom: 0;
                }

                .result-heading > strong {
                    border-radius: 999px;
                    padding: 5px 9px;
                    background: #fdebea;
                    color: var(--error);
                    font-size: 12px;
                    white-space: nowrap;
                }

                .eyebrow {
                    display: block;
                    margin-bottom: 4px;
                    color: var(--muted);
                    font-size: 12px;
                    font-weight: 700;
                    text-transform: uppercase;
                }

                .message {
                    margin: 0 0 12px;
                    font-weight: 700;
                    line-height: 1.45;
                }

                .result.ok {
                    border-left: 4px solid var(--ok);
                }

                .result.error {
                    border-left: 4px solid var(--error);
                }

                .metrics {
                    display: grid;
                    grid-template-columns: repeat(4, minmax(90px, 1fr));
                    gap: 10px;
                    margin-bottom: 12px;
                }

                .metric {
                    border: 1px solid var(--line);
                    border-radius: 6px;
                    padding: 10px;
                    background: #fbfdfd;
                }

                .metric span {
                    display: block;
                    color: var(--muted);
                    font-size: 12px;
                    margin-bottom: 4px;
                }

                .metric strong {
                    font-size: 20px;
                }

                .diagnostic-grid {
                    display: grid;
                    grid-template-columns: repeat(3, minmax(0, 1fr));
                    gap: 10px;
                    margin-bottom: 12px;
                }

                .diagnostic-grid div {
                    min-width: 0;
                    border: 1px solid var(--line);
                    border-radius: 6px;
                    padding: 10px;
                    background: #fffafa;
                }

                .diagnostic-grid span {
                    display: block;
                    margin-bottom: 4px;
                    color: var(--muted);
                    font-size: 12px;
                    font-weight: 700;
                }

                .diagnostic-grid strong {
                    display: block;
                    overflow-wrap: anywhere;
                    font-size: 13px;
                }

                .snippet {
                    border-radius: 6px;
                    background: var(--code);
                    color: #eaf4f4;
                    padding: 12px;
                    overflow: auto;
                }

                .snippet code {
                    display: block;
                    font: 13px Consolas, "Courier New", monospace;
                    line-height: 1.45;
                    white-space: pre;
                }

                .snippet .caret {
                    color: #ffcc66;
                }

                .empty-detail {
                    margin: 0;
                    padding: 12px;
                    border-radius: 6px;
                    background: #fff8e8;
                    color: #6b4b00;
                    line-height: 1.45;
                }

                .corrections {
                    margin-bottom: 12px;
                    border: 1px solid #cde7d6;
                    border-left: 4px solid var(--ok);
                    border-radius: 8px;
                    padding: 12px;
                    background: #f3fbf6;
                }

                .corrections-title {
                    margin-bottom: 8px;
                    color: var(--ok);
                    font-weight: 700;
                }

                .correction-item {
                    display: grid;
                    gap: 4px;
                    padding-top: 8px;
                    border-top: 1px solid #cde7d6;
                }

                .correction-item:first-of-type {
                    border-top: 0;
                    padding-top: 0;
                }

                .correction-item span {
                    color: var(--muted);
                    font-size: 12px;
                }

                details {
                    border-top: 1px solid var(--line);
                    padding-top: 10px;
                    margin-top: 10px;
                }

                summary {
                    cursor: pointer;
                    font-weight: 700;
                    color: var(--accent-dark);
                }

                pre {
                    margin: 10px 0 0;
                    padding: 12px;
                    max-height: 420px;
                    overflow: auto;
                    border-radius: 6px;
                    background: var(--code);
                    color: #eaf4f4;
                    font: 13px Consolas, "Courier New", monospace;
                    line-height: 1.45;
                    white-space: pre-wrap;
                }

                .empty {
                    min-height: 220px;
                    display: grid;
                    place-items: center;
                    border: 1px dashed var(--line);
                    border-radius: 8px;
                    color: var(--muted);
                    background: #ffffff;
                    text-align: center;
                    padding: 24px;
                }

                @media (max-width: 880px) {
                    .workspace {
                        grid-template-columns: 1fr;
                    }

                    .metrics {
                        grid-template-columns: repeat(2, 1fr);
                    }

                    .diagnostic-grid {
                        grid-template-columns: 1fr;
                    }
                }
            </style>
        </head>
        <body>
            <header>
                <div class="wrap topbar">
                    <h1>MiniCompiler</h1>
                    <div class="status">Frontend local</div>
                </div>
            </header>
            <main>
                <div class="wrap workspace">
                    <section class="panel">
                        <div class="tabs" role="tablist" aria-label="Entradas">
                            <button class="tab {{codeActive}}" type="button" data-tab="codigo">Codigo</button>
                            <button class="tab {{githubActive}}" type="button" data-tab="github">GitHub</button>
                            <button class="tab {{zipActive}}" type="button" data-tab="zip">ZIP</button>
                        </div>

                        <form class="pane {{codeActive}}" data-pane="codigo" method="post" action="/analisar/codigo">
                            <label for="source">Codigo fonte</label>
                            <textarea id="source" name="source" spellcheck="false">{{sampleCode}}</textarea>
                            <div class="actions">
                                <button type="submit">Compilar codigo</button>
                            </div>
                        </form>

                        <form class="pane {{githubActive}}" data-pane="github" method="post" action="/analisar/github">
                            <label for="githubUrl">Link do repositorio</label>
                            <input id="githubUrl" name="githubUrl" type="url" placeholder="https://github.com/usuario/repositorio">
                            <div class="actions">
                                <button type="submit">Analisar repositorio</button>
                            </div>
                        </form>

                        <form class="pane {{zipActive}}" data-pane="zip" method="post" action="/analisar/zip" enctype="multipart/form-data">
                            <label for="zipFile">Arquivo ZIP</label>
                            <input id="zipFile" name="zipFile" type="file" accept=".zip,application/zip,application/x-zip-compressed">
                            <div class="actions">
                                <button type="submit">Compilar ZIP</button>
                            </div>
                        </form>
                    </section>

                    <section>
                        {{(string.IsNullOrWhiteSpace(resultHtml) ? "<div class=\"empty\">Resultado da compilacao</div>" : resultHtml)}}
                    </section>
                </div>
            </main>
            <script>
                const tabs = document.querySelectorAll(".tab");
                const panes = document.querySelectorAll(".pane");

                tabs.forEach((tab) => {
                    tab.addEventListener("click", () => {
                        const target = tab.dataset.tab;

                        tabs.forEach((item) => item.classList.toggle("active", item === tab));
                        panes.forEach((pane) => pane.classList.toggle("active", pane.dataset.pane === target));
                    });
                });
            </script>
        </body>
        </html>
        """;
    }

    private static string FormatBytecode(IReadOnlyList<Instruction> instructions)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < instructions.Count; i++)
        {
            builder.AppendLine($"{i:D4}: {instructions[i]}");
        }

        return builder.ToString();
    }

    private static string Metric(string title, int value)
    {
        return $"<div class=\"metric\"><span>{Escape(title)}</span><strong>{value}</strong></div>";
    }

    private static string Metric(string title, string value)
    {
        return $"<div class=\"metric\"><span>{Escape(title)}</span><strong>{Escape(value)}</strong></div>";
    }

    private static IResult Html(string html)
    {
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static string Escape(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string DisplayName(string value)
    {
        if (value.StartsWith("<", StringComparison.Ordinal))
        {
            return value;
        }

        return Path.GetFileName(value);
    }

    private static string ReadUrl(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--url", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return DefaultUrl;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Arquivo temporario: se o Windows estiver segurando o handle, ele sera apagado depois pelo sistema.
        }
    }
}
