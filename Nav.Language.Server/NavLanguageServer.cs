#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using StreamJsonRpc;

using Lsp = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Server;

/// <summary>
/// LSP-Server fuer die Nav-Sprache. Erste Ausbaustufe: Lebenszyklus (initialize/shutdown/exit),
/// Dokument-Sync (didOpen/didChange/didClose, Full-Sync) und <c>textDocument/publishDiagnostics</c>.
/// Workspace-Discovery, Overlay-Modell und weitere Features folgen.
/// </summary>
class NavLanguageServer {

    readonly JsonRpc _rpc;

    public NavLanguageServer(JsonRpc rpc) {
        _rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
    }

    [JsonRpcMethod(Lsp.Methods.InitializeName, UseSingleObjectParameterDeserialization = true)]
    public Lsp.InitializeResult Initialize(Lsp.InitializeParams param) {

        return new Lsp.InitializeResult {
            Capabilities = new Lsp.ServerCapabilities {
                TextDocumentSync = new Lsp.TextDocumentSyncOptions {
                    OpenClose = true,
                    Change    = Lsp.TextDocumentSyncKind.Full
                }
            }
        };
    }

    [JsonRpcMethod(Lsp.Methods.InitializedName, UseSingleObjectParameterDeserialization = true)]
    public void Initialized(Lsp.InitializedParams param) {
        // Aktuell nichts zu tun.
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentDidOpenName, UseSingleObjectParameterDeserialization = true)]
    public Task DidOpenAsync(Lsp.DidOpenTextDocumentParams param) {
        return PublishDiagnosticsAsync(param.TextDocument.Uri, param.TextDocument.Text);
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentDidChangeName, UseSingleObjectParameterDeserialization = true)]
    public Task DidChangeAsync(Lsp.DidChangeTextDocumentParams param) {

        // Full-Sync: die letzte Aenderung enthaelt den vollstaendigen Dokumentinhalt.
        var text = param.ContentChanges.Length > 0
            ? param.ContentChanges[^1].Text
            : string.Empty;

        return PublishDiagnosticsAsync(param.TextDocument.Uri, text);
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentDidCloseName, UseSingleObjectParameterDeserialization = true)]
    public Task DidCloseAsync(Lsp.DidCloseTextDocumentParams param) {
        // Diagnostics fuer das geschlossene Dokument leeren.
        return PublishAsync(param.TextDocument.Uri, Array.Empty<Lsp.Diagnostic>());
    }

    [JsonRpcMethod(Lsp.Methods.ShutdownName)]
    public object? Shutdown() => null;

    [JsonRpcMethod(Lsp.Methods.ExitName)]
    public void Exit() {
        Environment.Exit(0);
    }

    Task PublishDiagnosticsAsync(Uri uri, string text) {

        var filePath       = uri.IsFile ? uri.LocalPath : uri.ToString();
        var navDiagnostics = DiagnosticsComputer.Compute(filePath, text, CancellationToken.None);
        var lspDiagnostics = navDiagnostics.Select(LspMapper.ToLsp).ToArray();

        return PublishAsync(uri, lspDiagnostics);
    }

    Task PublishAsync(Uri uri, Lsp.Diagnostic[] diagnostics) {

        var publishParams = new Lsp.PublishDiagnosticParams {
            Uri         = uri,
            Diagnostics = diagnostics
        };

        return _rpc.NotifyWithParameterObjectAsync(Lsp.Methods.TextDocumentPublishDiagnosticsName, publishParams);
    }
}
