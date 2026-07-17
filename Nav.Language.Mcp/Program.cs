#region Using Directives

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ModelContextProtocol;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp;

static class Program {

    static async Task Main(string[] args) {

        var builder = Host.CreateApplicationBuilder(args);

        // stdout ist exklusiv fürs MCP-Protokoll (JSON-RPC über stdio) reserviert. Alle Logs MÜSSEN nach
        // stderr, sonst zerstören sie die Protokoll-Frames — dasselbe Prinzip wie beim LSP-Server.
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        // MCP-Hosting/Per-Request-Logs sind sehr gesprächig — auf Warning drosseln (stderr sauber halten).
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Workspace-Root: erstes Kommandozeilen-Argument (vom MCP-Client gesetzt) oder das aktuelle
        // Arbeitsverzeichnis (Claude Code startet MCP-Server typischerweise im Projektverzeichnis).
        var root = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : Directory.GetCurrentDirectory();

        Console.Error.WriteLine($"[nav-mcp] Server gestartet (root: {root}), warte auf MCP über stdio.");

        builder.Services.AddSingleton(new NavMcpWorkspace(root));

        // Getrimmt ist der reflektionsbasierte System.Text.Json-Serializer aus → die Tool-Ergebnis-DTOs
        // brauchen source-generierte JSON-Metadaten (sonst NotSupportedException beim Schema-Aufbau).
        // Den Nav-Kontext mit Vorrang in eine Kopie der SDK-Default-Optionen einhängen (Casing/Ignore
        // bleiben dadurch identisch zum untrimmt-reflektierten Pfad).
        var toolSerializerOptions = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
        toolSerializerOptions.TypeInfoResolverChain.Insert(0, NavMcpJsonSerializerContext.Default);

        // Tools statisch (reflektionsfrei) registrieren — WithToolsFromAssembly() wäre trim-unsicher
        // (IL2026). WithTools<T>() ist trim-sicher: T ist statisch bekannt, der MCP-SDK-Sourcegen erzeugt
        // die Metadaten dafür. Beim Ergänzen eines neuen Tools hier mit eintragen.
        builder.Services
               .AddMcpServer()
               .WithStdioServerTransport()
               .WithTools<Tools.NavValidateTool>(toolSerializerOptions)
               .WithTools<Tools.NavDiagnosticsTool>(toolSerializerOptions)
               .WithTools<Tools.NavOutlineTool>(toolSerializerOptions)
               .WithTools<Tools.NavWorkspaceTool>(toolSerializerOptions)
               .WithTools<Tools.NavFindSymbolTool>(toolSerializerOptions)
               .WithTools<Tools.NavGotoTool>(toolSerializerOptions)
               .WithTools<Tools.NavReferencesTool>(toolSerializerOptions)
               .WithTools<Tools.NavRenameTool>(toolSerializerOptions)
               .WithTools<Tools.NavCodeActionsTool>(toolSerializerOptions)
               .WithTools<Tools.NavFormatTool>(toolSerializerOptions)
               .WithTools<Tools.NavGrammarTool>(toolSerializerOptions)
               .WithTools<Tools.NavPreviewCodegenTool>(toolSerializerOptions)
               .WithTools<Tools.NavCallHierarchyTool>(toolSerializerOptions)
               .WithTools<Tools.NavExitUsagesTool>(toolSerializerOptions);

        await builder.Build().RunAsync();
    }
}
