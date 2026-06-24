#region Using Directives

using System;
using System.IO;
using System.Threading.Tasks;

using Newtonsoft.Json.Serialization;

using StreamJsonRpc;

#endregion

namespace Pharmatechnik.Nav.Language.Server;

static class Program {

    static async Task Main(string[] args) {

        var stdin  = Console.OpenStandardInput();
        var stdout = Console.OpenStandardOutput();

        // stdout ist exklusiv für JSON-RPC reserviert. Versehentliche Console-Ausgaben (z.B. aus der
        // Engine) nach stderr umleiten, damit sie die LSP-Frames nicht zerstören.
        Console.SetOut(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        Console.Error.WriteLine("[nav-lsp] Server gestartet, warte auf JSON-RPC über stdio.");

        // LSP nutzt header-delimitierte JSON-RPC-Nachrichten. Die LSP-Protokoll-DTOs
        // (Microsoft.VisualStudio.LanguageServer.Protocol) sind Newtonsoft-annotiert, daher der
        // JsonMessageFormatter (Newtonsoft) statt des System.Text.Json-Formatters.
#pragma warning disable CS0618 // JsonMessageFormatter ist als "wird in 3.0 entfernt" markiert.
        var formatter = new JsonMessageFormatter();
#pragma warning restore CS0618

        // Manche LSP-DTOs (z.B. SemanticTokensOptions) tragen keine expliziten DataMember-Namen und
        // würden in PascalCase serialisiert ("Legend"/"Full"), was LSP-Clients nicht verstehen.
        // Unbenannte Properties auf camelCase abbilden, explizit benannte (DataMember) unangetastet lassen.
        formatter.JsonSerializer.ContractResolver = new DefaultContractResolver {
            NamingStrategy = new CamelCaseNamingStrategy { OverrideSpecifiedNames = false }
        };
        var handler = new HeaderDelimitedMessageHandler(stdout, stdin, formatter);
        var rpc     = new JsonRpc(handler);

        var server = new NavLanguageServer(rpc);
        rpc.AddLocalRpcTarget(server);

        rpc.StartListening();

        await rpc.Completion;
    }
}
