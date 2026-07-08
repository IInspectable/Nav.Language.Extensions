#region Using Directives

using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        builder.Services
               .AddMcpServer()
               .WithStdioServerTransport()
               .WithToolsFromAssembly();

        await builder.Build().RunAsync();
    }
}
