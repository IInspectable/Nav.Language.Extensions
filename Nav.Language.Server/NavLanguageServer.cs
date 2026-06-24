#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using StreamJsonRpc;

using Pharmatechnik.Nav.Language.GoTo;
using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Language.QuickInfo;
using Pharmatechnik.Nav.Language.References;
using Pharmatechnik.Nav.Language.FindReferences;
using Pharmatechnik.Nav.Utilities.IO;

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
                },
                SemanticTokensOptions = new Lsp.SemanticTokensOptions {
                    Legend = new Lsp.SemanticTokensLegend {
                        TokenTypes     = SemanticTokensBuilder.TokenTypes,
                        TokenModifiers = SemanticTokensBuilder.TokenModifiers
                    },
                    Full = true
                },
                DocumentSymbolProvider    = true,
                DefinitionProvider        = true,
                ReferencesProvider        = true,
                DocumentHighlightProvider = true,
                HoverProvider             = true
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
        return UpdateOverlayAndPublishAsync(param.TextDocument.Uri, param.TextDocument.Text);
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentDidChangeName, UseSingleObjectParameterDeserialization = true)]
    public Task DidChangeAsync(Lsp.DidChangeTextDocumentParams param) {

        // Full-Sync: die letzte Änderung enthält den vollständigen Dokumentinhalt.
        var text = param.ContentChanges.Length > 0
            ? param.ContentChanges[^1].Text
            : string.Empty;

        return UpdateOverlayAndPublishAsync(param.TextDocument.Uri, text);
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentDidCloseName, UseSingleObjectParameterDeserialization = true)]
    public Task DidCloseAsync(Lsp.DidCloseTextDocumentParams param) {

        var uri            = param.TextDocument.Uri;
        var normalizedPath = NavUri.ToNormalizedPath(uri);
        if (normalizedPath == null) {
            return Task.CompletedTask;
        }

        // Overlay verwerfen — die Wahrheit liegt wieder auf Platte. Diagnostics von Platte neu berechnen.
        _workspace.Close(normalizedPath);
        return PublishDocumentAsync(uri);
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentDocumentSymbolName, UseSingleObjectParameterDeserialization = true)]
    public Lsp.DocumentSymbol[] DocumentSymbols(Lsp.DocumentSymbolParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Lsp.DocumentSymbol>();
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);

        return unit == null
            ? Array.Empty<Lsp.DocumentSymbol>()
            : DocumentSymbolBuilder.Build(unit);
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentDefinitionName, UseSingleObjectParameterDeserialization = true)]
    public Lsp.Location[] Definition(Lsp.TextDocumentPositionParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Lsp.Location>();
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);
        if (unit == null) {
            return Array.Empty<Lsp.Location>();
        }

        // LSP-Position (Zeile/Zeichen) → Offset → Nav→Nav-Ziele (engine-seitig, VS-frei).
        var offset  = LspMapper.ToOffset(unit.Syntax.SyntaxTree.SourceText, param.Position);
        var targets = NavGoToService.GetGoToLocations(unit, offset);

        return targets.Select(LspMapper.ToLocation)
                      .OfType<Lsp.Location>()
                      .ToArray();
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentReferencesName, UseSingleObjectParameterDeserialization = true)]
    public async Task<Lsp.Location[]> References(Lsp.ReferenceParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Lsp.Location>();
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);
        if (unit == null) {
            return Array.Empty<Lsp.Location>();
        }

        var offset = LspMapper.ToOffset(unit.Syntax.SyntaxTree.SourceText, param.Position);
        var origin = NavReferenceService.FindSymbol(unit, offset);
        if (origin == null) {
            return Array.Empty<Lsp.Location>();
        }

        // Solution-weite Referenzsuche über die Engine-API; der Collector sammelt nur die Locations.
        var includeDeclaration = param.Context?.IncludeDeclaration ?? true;
        var collector          = new ReferenceCollector(includeDeclaration, CancellationToken.None);
        var args               = new FindReferencesArgs(origin, unit, _workspace.Solution, collector);

        await ReferenceFinder.FindReferencesAsync(args);

        return collector.Locations
                        .Select(LspMapper.ToLocation)
                        .OfType<Lsp.Location>()
                        .ToArray();
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentDocumentHighlightName, UseSingleObjectParameterDeserialization = true)]
    public Lsp.DocumentHighlight[] DocumentHighlight(Lsp.TextDocumentPositionParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Lsp.DocumentHighlight>();
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);
        if (unit == null) {
            return Array.Empty<Lsp.DocumentHighlight>();
        }

        var offset  = LspMapper.ToOffset(unit.Syntax.SyntaxTree.SourceText, param.Position);
        var symbols = NavReferenceService.GetHighlightSymbols(unit, offset);

        // documentHighlight ist per Definition dateilokal; das erste Symbol ist die Deklaration (Write).
        var normalizedFile = PathHelper.NormalizePath(filePath);
        var highlights     = new List<Lsp.DocumentHighlight>();

        for (var i = 0; i < symbols.Count; i++) {

            var location = symbols[i].Location;
            if (PathHelper.NormalizePath(location.FilePath) != normalizedFile) {
                continue;
            }

            highlights.Add(new Lsp.DocumentHighlight {
                Range = LspMapper.ToRange(location),
                Kind  = i == 0 ? Lsp.DocumentHighlightKind.Write : Lsp.DocumentHighlightKind.Read
            });
        }

        return highlights.ToArray();
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentHoverName, UseSingleObjectParameterDeserialization = true)]
    public Lsp.Hover? Hover(Lsp.TextDocumentPositionParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return null;
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);
        if (unit == null) {
            return null;
        }

        var offset = LspMapper.ToOffset(unit.Syntax.SyntaxTree.SourceText, param.Position);
        var info   = NavHoverService.GetHover(unit, offset);
        if (info == null) {
            return null;
        }

        var content = BuildHoverContent(info);
        if (content == null) {
            return null;
        }

        var hover = new Lsp.Hover {
            // Die Signatur als Nav-Codeblock; VS Code rendert sie monospace.
            Contents = new Lsp.MarkupContent {
                Kind  = Lsp.MarkupKind.Markdown,
                Value = $"```nav\n{content}\n```"
            }
        };

        if (info.Location != null) {
            hover.Range = LspMapper.ToRange(info.Location);
        }

        return hover;
    }

    /// <summary>
    /// Baut den Hover-Text: die Signatur des Symbols und — bei Choices/Edges — darunter die Liste der
    /// erreichbaren Knoten (je Zeile „Verb Zielsignatur"), wie die VS-QuickInfo sie zeigt. Null, wenn
    /// nichts Anzeigbares übrig bleibt.
    /// </summary>
    static string? BuildHoverContent(NavHoverInfo info) {

        var signature = string.Concat(info.DisplayParts.Select(p => p.Text));
        var hasHeader = !string.IsNullOrWhiteSpace(signature);

        var sb = new System.Text.StringBuilder();
        if (hasHeader) {
            sb.Append(signature);
        }

        foreach (var call in info.Calls) {

            var target = string.Concat(call.Node.ToDisplayParts().Select(p => p.Text));
            if (string.IsNullOrWhiteSpace(target)) {
                continue;
            }

            if (sb.Length > 0) {
                sb.Append('\n');
            }

            // Unter einer Signatur (Choice) eingerückt; ohne Header (Edge-Mode) bündig.
            if (hasHeader) {
                sb.Append("    ");
            }

            // Das getippte Pfeil-Token (-->, o->, *->, ==>) statt des ausgeschriebenen Verbs.
            sb.Append(call.EdgeMode.Name).Append(' ').Append(target);
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentSemanticTokensFullName, UseSingleObjectParameterDeserialization = true)]
    public Lsp.SemanticTokens SemanticTokensFull(Lsp.SemanticTokensParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return new Lsp.SemanticTokens { Data = Array.Empty<int>() };
        }

        var syntaxTree = _workspace.GetSyntaxTree(filePath, CancellationToken.None);

        return new Lsp.SemanticTokens {
            Data = syntaxTree == null ? Array.Empty<int>() : SemanticTokensBuilder.Encode(syntaxTree)
        };
    }

    [JsonRpcMethod(Lsp.Methods.ShutdownName)]
    public object? Shutdown() => null;

    [JsonRpcMethod(Lsp.Methods.ExitName)]
    public void Exit() {
        Environment.Exit(0);
    }

    Task UpdateOverlayAndPublishAsync(Uri uri, string text) {

        var normalizedPath = NavUri.ToNormalizedPath(uri);
        if (normalizedPath == null) {
            return Task.CompletedTask;
        }

        _workspace.OpenOrUpdate(normalizedPath, text);
        return PublishDocumentAsync(uri);
    }

    Task PublishDocumentAsync(Uri uri) {

        var filePath = NavUri.ToFilePath(uri);
        if (filePath == null) {
            return Task.CompletedTask;
        }

        var navDiagnostics = _workspace.GetDiagnostics(filePath, CancellationToken.None);
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
