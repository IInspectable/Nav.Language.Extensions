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
/// LSP-Server für die Nav-Sprache. Erste Ausbaustufe: Lebenszyklus (initialize/shutdown/exit),
/// Dokument-Sync (didOpen/didChange/didClose, Full-Sync) und <c>textDocument/publishDiagnostics</c>.
/// Workspace-Discovery, Overlay-Modell und weitere Features folgen.
/// </summary>
class NavLanguageServer {

    readonly JsonRpc      _rpc;
    readonly NavWorkspace _workspace = new();

    string? _rootPath;

    public NavLanguageServer(JsonRpc rpc) {
        _rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
    }

    [JsonRpcMethod(Lsp.Methods.InitializeName, UseSingleObjectParameterDeserialization = true)]
    public Lsp.InitializeResult Initialize(Lsp.InitializeParams param) {

        _rootPath = ResolveRootPath(param);

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
    public async Task InitializedAsync(Lsp.InitializedParams param) {

        // Workspace selbst entdecken (LSP schiebt keine Dateiliste): alle *.nav unterhalb der rootUri
        // globben, Cross-File-Modell aufbauen und Diagnostics für jede Datei veröffentlichen.
        await _workspace.LoadAsync(_rootPath, CancellationToken.None);
        await _workspace.PublishAllDiagnosticsAsync(PublishAsync, CancellationToken.None);
    }

    static string? ResolveRootPath(Lsp.InitializeParams param) {

        if (param.RootUri is { IsAbsoluteUri: true } rootUri && rootUri.IsFile) {
            return rootUri.LocalPath;
        }

        // RootPath ist zwar deprecated, aber als Fallback für ältere Clients nützlich.
#pragma warning disable CS0618
        return string.IsNullOrEmpty(param.RootPath) ? null : param.RootPath;
#pragma warning restore CS0618
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentDidOpenName, UseSingleObjectParameterDeserialization = true)]
    public Task DidOpenAsync(Lsp.DidOpenTextDocumentParams param) {
        return PublishDiagnosticsAsync(param.TextDocument.Uri, param.TextDocument.Text);
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentDidChangeName, UseSingleObjectParameterDeserialization = true)]
    public Task DidChangeAsync(Lsp.DidChangeTextDocumentParams param) {

        // Full-Sync: die letzte Änderung enthält den vollständigen Dokumentinhalt.
        var text = param.ContentChanges.Length > 0
            ? param.ContentChanges[^1].Text
            : string.Empty;

        return PublishDiagnosticsAsync(param.TextDocument.Uri, text);
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentDidCloseName, UseSingleObjectParameterDeserialization = true)]
    public Task DidCloseAsync(Lsp.DidCloseTextDocumentParams param) {
        // Diagnostics für das geschlossene Dokument leeren.
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
