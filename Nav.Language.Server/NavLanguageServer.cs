#region Using Directives

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using StreamJsonRpc;

using Newtonsoft.Json.Linq;

using Pharmatechnik.Nav.Language.GoTo;
using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Language.Completion;
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
                HoverProvider             = true,
                FoldingRangeProvider      = true,
                CodeLensProvider          = new Lsp.CodeLensOptions {
                    // Die Marken liefern wir leer (nur Position); Beschriftung + Klick-Command erst
                    // träge über codeLens/resolve, weil die solution-weite Referenzsuche teuer ist.
                    ResolveProvider = true
                },
                CompletionProvider        = new Lsp.CompletionOptions {
                    // Buchstaben lösen im Client automatisch aus; ':' für Exit-Connection-Points,
                    // '-' für den Beginn einer Edge (-->), '"' sowie die Pfadtrenner für die
                    // Pfad-Vervollständigung in taskref (Pfadtrenner aktualisieren Liste + Replace-Range).
                    TriggerCharacters = new[] { ":", "-", "\"", "/", "\\" },
                    ResolveProvider   = false
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
            // NICHT rootUri.LocalPath: VS Code/VS prozent-kodieren den Laufwerks-Doppelpunkt
            // (file:///d%3A/...), wofür LocalPath einen kaputten Pfad "/d:/..." liefert → Directory.Exists
            // schlägt fehl → leere Solution → keine Pfad-Vervollständigung. NavUri.ToFilePath behebt das.
            return NavUri.ToFilePath(rootUri);
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

    [JsonRpcMethod(Lsp.Methods.TextDocumentFoldingRangeName, UseSingleObjectParameterDeserialization = true)]
    public Lsp.FoldingRange[] FoldingRange(Lsp.FoldingRangeParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Lsp.FoldingRange>();
        }

        var syntaxTree = _workspace.GetSyntaxTree(filePath, CancellationToken.None);

        return syntaxTree == null
            ? Array.Empty<Lsp.FoldingRange>()
            : FoldingRangeBuilder.Build(syntaxTree);
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentCodeLensName, UseSingleObjectParameterDeserialization = true)]
    public Lsp.CodeLens[] CodeLens(Lsp.CodeLensParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Lsp.CodeLens>();
        }

        // Nur Positionen — rein syntaktisch und ohne Referenzsuche (die folgt träge in resolve).
        var syntaxTree = _workspace.GetSyntaxTree(filePath, CancellationToken.None);

        return syntaxTree == null
            ? Array.Empty<Lsp.CodeLens>()
            : CodeLensBuilder.Build(syntaxTree, param.TextDocument.Uri);
    }

    [JsonRpcMethod(Lsp.Methods.CodeLensResolveName, UseSingleObjectParameterDeserialization = true)]
    public async Task<Lsp.CodeLens> CodeLensResolve(Lsp.CodeLens codeLens) {

        // Data kommt über die Leitung als JObject zurück (Newtonsoft) → Dokument + Offset herauslesen.
        var data = (codeLens.Data as JObject)?.ToObject<CodeLensData>();
        if (data == null || string.IsNullOrEmpty(data.Uri)) {
            return codeLens;
        }

        var filePath = NavUri.ToFilePath(new Uri(data.Uri));
        if (filePath == null) {
            return codeLens;
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);
        if (unit == null) {
            return codeLens;
        }

        var origin = NavReferenceService.FindSymbol(unit, data.Offset);
        if (origin == null) {
            return codeLens;
        }

        // Solution-weite Referenzsuche (ohne die Deklaration selbst) — wie der References-Handler.
        var collector = new ReferenceCollector(includeDeclaration: false, CancellationToken.None);
        var args      = new FindReferencesArgs(origin, unit, _workspace.Solution, collector);

        await ReferenceFinder.FindReferencesAsync(args);

        var locations = collector.Locations
                                 .Select(LspMapper.ToLocation)
                                 .OfType<Lsp.Location>()
                                 .ToArray();

        var count = locations.Length;

        codeLens.Command = new Lsp.Command {
            Title = count == 1 ? "1 Verweis" : $"{count} Verweise"
        };

        // Beim Klick das Peek-Fenster öffnen. editor.action.showReferences erwartet echte vscode-Typen,
        // die der LanguageClient für freie Command-Argumente NICHT konvertiert — daher der Umweg über den
        // extension-seitigen Command nav.showReferences, der die JSON-Argumente umwandelt und weiterreicht.
        if (count > 0) {
            codeLens.Command.CommandIdentifier = "nav.showReferences";
            codeLens.Command.Arguments = new object[] { data.Uri, codeLens.Range.Start, locations };
        }

        return codeLens;
    }

    [JsonRpcMethod(Lsp.Methods.TextDocumentCompletionName, UseSingleObjectParameterDeserialization = true)]
    public Lsp.CompletionItem[] Completion(Lsp.CompletionParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Lsp.CompletionItem>();
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);
        if (unit == null) {
            return Array.Empty<Lsp.CompletionItem>();
        }

        var sourceText = unit.Syntax.SyntaxTree.SourceText;
        var offset     = LspMapper.ToOffset(sourceText, param.Position);
        var items      = NavCompletionService.GetCompletions(unit, offset, _workspace.Solution);

        // SortText nach Index, damit die vom Service vorgegebene Reihenfolge (unverbundene Exits /
        // unreferenzierte Knoten zuerst, bzw. Elternverzeichnis → Verzeichnisse → Dateien) im Client
        // erhalten bleibt — sonst sortiert dieser alphabetisch.
        var result = new Lsp.CompletionItem[items.Count];
        for (var i = 0; i < items.Count; i++) {

            var item = items[i];

            var lspItem = new Lsp.CompletionItem {
                Label    = item.Label,
                Kind     = ToCompletionItemKind(item.Kind),
                SortText = i.ToString("D4")
            };

            if (item.Detail != null) {
                lspItem.Detail = item.Detail;
            }

            // Pfad-Vorschläge ersetzen den gesamten Inhalt zwischen den "" (relativer Pfad ≠ Anzeigename),
            // daher ein expliziter TextEdit statt des Default-Wortersatzes des Clients. Eingefügt wird der
            // relative Pfad; GEFILTERT wird aber — wie in VS — über den DATEINAMEN (Label), nicht über den
            // relativen Pfad: Der Ersetzungsbereich umfasst den gesamten String-Inhalt, sodass der Client den
            // getippten Text gegen den FilterText matcht. Stünde dort der relative Pfad (z.B. "..\..\Foo\Bar.nav"),
            // fände das Tippen eines bloßen Dateinamens ("Bar") nichts, weil das führende "..\..\" den Fuzzy-Match
            // dominiert. Der Dateiname als FilterText findet die Datei zuverlässig allein durch ihren Namen.
            if (item.ReplacementExtent is { } extent) {
                lspItem.TextEdit = new Lsp.TextEdit {
                    NewText = item.InsertText,
                    Range   = LspMapper.ToRange(sourceText.GetLocation(extent))
                };
                lspItem.FilterText = item.Label;
            } else if (item.InsertText != item.Label) {
                lspItem.InsertText = item.InsertText;
            }

            result[i] = lspItem;
        }

        return result;
    }

    static Lsp.CompletionItemKind ToCompletionItemKind(NavCompletionItemKind kind) => kind switch {
        NavCompletionItemKind.Keyword         => Lsp.CompletionItemKind.Keyword,
        NavCompletionItemKind.Task            => Lsp.CompletionItemKind.Class,
        NavCompletionItemKind.ConnectionPoint => Lsp.CompletionItemKind.Field,
        NavCompletionItemKind.Choice          => Lsp.CompletionItemKind.EnumMember,
        NavCompletionItemKind.GuiNode         => Lsp.CompletionItemKind.Interface,
        NavCompletionItemKind.File            => Lsp.CompletionItemKind.File,
        NavCompletionItemKind.Folder          => Lsp.CompletionItemKind.Folder,
        _                                     => Lsp.CompletionItemKind.Variable
    };

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
