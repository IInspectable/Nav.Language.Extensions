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
using Pharmatechnik.Nav.Language.CallHierarchy;
using Pharmatechnik.Nav.Language.Rename;
using Pharmatechnik.Nav.Language.CodeFixes;
using Pharmatechnik.Nav.Language.CodeActions;
using Pharmatechnik.Nav.Language.Completion;
using Pharmatechnik.Nav.Language.QuickInfo;
using Pharmatechnik.Nav.Language.References;
using Pharmatechnik.Nav.Language.FindReferences;
using Pharmatechnik.Nav.Utilities.IO;

using Protocol = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Lsp;

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

    [JsonRpcMethod(Protocol.Methods.InitializeName, UseSingleObjectParameterDeserialization = true)]
    public Protocol.InitializeResult Initialize(Protocol.InitializeParams param) {

        _rootPath = ResolveRootPath(param);

        return new Protocol.InitializeResult {
            // NavServerCapabilities ergänzt callHierarchyProvider, das das Protokoll-Paket 17.2.8 nicht kennt.
            Capabilities = new NavServerCapabilities {
                CallHierarchyProvider = true,
                TextDocumentSync = new Protocol.TextDocumentSyncOptions {
                    OpenClose = true,
                    Change    = Protocol.TextDocumentSyncKind.Full
                },
                SemanticTokensOptions = new Protocol.SemanticTokensOptions {
                    Legend = new Protocol.SemanticTokensLegend {
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
                RenameProvider            = true,
                CodeActionProvider        = new Protocol.CodeActionOptions {
                    // QuickFix (ErrorFix/StyleFix) + Refactor (Introduce Choice). Die Aktionen liefern
                    // ihre WorkspaceEdit direkt mit — kein codeAction/resolve nötig.
                    CodeActionKinds = new[] { Protocol.CodeActionKind.QuickFix, Protocol.CodeActionKind.Refactor }
                },
                CodeLensProvider          = new Protocol.CodeLensOptions {
                    // Die Marken liefern wir leer (nur Position); Beschriftung + Klick-Command erst
                    // träge über codeLens/resolve, weil die solution-weite Referenzsuche teuer ist.
                    ResolveProvider = true
                },
                CompletionProvider        = new Protocol.CompletionOptions {
                    // Buchstaben lösen im Client automatisch aus; ':' für Exit-Connection-Points,
                    // '-' für den Beginn einer Edge (-->), '"' sowie die Pfadtrenner für die
                    // Pfad-Vervollständigung in taskref (Pfadtrenner aktualisieren Liste + Replace-Range).
                    TriggerCharacters = new[] { ":", "-", "\"", "/", "\\" },
                    ResolveProvider   = false
                }
            }
        };
    }

    [JsonRpcMethod(Protocol.Methods.InitializedName, UseSingleObjectParameterDeserialization = true)]
    public async Task InitializedAsync(Protocol.InitializedParams param) {

        // Workspace selbst entdecken (LSP schiebt keine Dateiliste): alle *.nav unterhalb der rootUri
        // globben, Cross-File-Modell aufbauen und Diagnostics für jede Datei veröffentlichen.
        await _workspace.LoadAsync(_rootPath, CancellationToken.None);
        await _workspace.PublishAllDiagnosticsAsync(PublishAsync, CancellationToken.None);
    }

    static string? ResolveRootPath(Protocol.InitializeParams param) {

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

    [JsonRpcMethod(Protocol.Methods.TextDocumentDidOpenName, UseSingleObjectParameterDeserialization = true)]
    public Task DidOpenAsync(Protocol.DidOpenTextDocumentParams param) {
        return UpdateOverlayAndPublishAsync(param.TextDocument.Uri, param.TextDocument.Text);
    }

    [JsonRpcMethod(Protocol.Methods.TextDocumentDidChangeName, UseSingleObjectParameterDeserialization = true)]
    public Task DidChangeAsync(Protocol.DidChangeTextDocumentParams param) {

        // Full-Sync: die letzte Änderung enthält den vollständigen Dokumentinhalt.
        var text = param.ContentChanges.Length > 0
            ? param.ContentChanges[^1].Text
            : string.Empty;

        // Bei einer echten Änderung zusätzlich die (transitiv) inkludierenden Dateien neu diagnostizieren —
        // deren Cross-File-Diagnostics können sich ändern, obwohl sie selbst nicht editiert wurden.
        return UpdateOverlayAndPublishAsync(param.TextDocument.Uri, text, publishDependents: true);
    }

    [JsonRpcMethod(Protocol.Methods.TextDocumentDidCloseName, UseSingleObjectParameterDeserialization = true)]
    public Task DidCloseAsync(Protocol.DidCloseTextDocumentParams param) {

        var uri            = param.TextDocument.Uri;
        var normalizedPath = NavUri.ToNormalizedPath(uri);
        if (normalizedPath == null) {
            return Task.CompletedTask;
        }

        // Overlay verwerfen — die Wahrheit liegt wieder auf Platte. Diagnostics von Platte neu berechnen.
        // Der Inhalt fällt vom (ggf. ungespeicherten) Overlay auf die Platte zurück, deshalb können sich
        // auch hier die Diagnostics der inkludierenden Dateien ändern.
        _workspace.Close(normalizedPath);
        return PublishDocumentAndDependentsAsync(uri);
    }

    [JsonRpcMethod(Protocol.Methods.WorkspaceDidChangeWatchedFilesName, UseSingleObjectParameterDeserialization = true)]
    public async Task DidChangeWatchedFilesAsync(Protocol.DidChangeWatchedFilesParams param) {

        if (param.Changes == null) {
            return;
        }

        var anyCreated      = false;
        var anyIgnoreChanged = false;

        foreach (var change in param.Changes) {

            var uri            = change.Uri;
            var normalizedPath = NavUri.ToNormalizedPath(uri);
            var filePath       = NavUri.ToFilePath(uri);
            if (normalizedPath == null || filePath == null) {
                continue;
            }

            // .navignore angelegt/geändert/gelöscht: nicht als Nav-Dokument behandeln, sondern nur merken —
            // die Ignore-Regeln werden danach einmal gebündelt neu geladen und alle Diagnostics neu publiziert.
            if (IsNavIgnoreFile(filePath)) {
                anyIgnoreChanged = true;
                continue;
            }

            // Offene Dokumente: das Overlay schlägt die Platte (LSP-Prinzip). Den Disk-Event ignorieren —
            // beim Schließen (didClose) wird ohnehin von Platte neu gelesen.
            if (_workspace.IsOpen(normalizedPath)) {
                continue;
            }

            // Der gecachte Platten-Syntax ist nun veraltet → invalidieren, damit frisch gelesen wird.
            _workspace.InvalidateDiskCache(normalizedPath);

            switch (change.FileChangeType) {
                case Protocol.FileChangeType.Created:
                    _workspace.AddSolutionFile(filePath);
                    anyCreated = true;
                    break;
                case Protocol.FileChangeType.Deleted:
                    _workspace.RemoveSolutionFile(normalizedPath);
                    break;
            }

            // Die geänderte/gelöschte Datei selbst neu diagnostizieren (gelöscht → leere Diagnostics =
            // löscht die Anzeige beim Client) und die (transitiv) inkludierenden Dateien gleich mit.
            await PublishDocumentAsync(uri);
            await _workspace.PublishDependentsAsync(filePath, PublishAsync, CancellationToken.None);
        }

        // Eine geänderte .navignore betrifft potenziell beliebig viele Dateien (neu ignoriert → Diagnostics
        // löschen; nicht mehr ignoriert → Diagnostics wieder anzeigen). Regeln neu laden und die gesamte
        // Solution einmal neu diagnostizieren — deckt beide Richtungen ab.
        if (anyIgnoreChanged) {
            _workspace.ReloadIgnore();
            await _workspace.PublishAllDiagnosticsAsync(PublishAsync, CancellationToken.None);
            return;
        }

        // Neu angelegte Dateien können von Dateien inkludiert werden, deren bisher fehlgeschlagene
        // taskref-Direktive KEINE Graph-Kante hinterlassen hat (unaufgelöste Includes werden nicht
        // verzeichnet). Solche Inkludierer findet der Abhängigkeitsgraph nicht — daher beim Anlegen einmalig
        // (gebündelt für den ganzen Batch) die gesamte Solution neu diagnostizieren.
        if (anyCreated) {
            await _workspace.PublishAllDiagnosticsAsync(PublishAsync, CancellationToken.None);
        }
    }

    static bool IsNavIgnoreFile(string filePath) =>
        string.Equals(System.IO.Path.GetFileName(filePath), ".navignore", StringComparison.OrdinalIgnoreCase);

    [JsonRpcMethod(Protocol.Methods.TextDocumentDocumentSymbolName, UseSingleObjectParameterDeserialization = true)]
    public Protocol.DocumentSymbol[] DocumentSymbols(Protocol.DocumentSymbolParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Protocol.DocumentSymbol>();
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);

        return unit == null
            ? Array.Empty<Protocol.DocumentSymbol>()
            : DocumentSymbolBuilder.Build(unit);
    }

    [JsonRpcMethod(Protocol.Methods.TextDocumentDefinitionName, UseSingleObjectParameterDeserialization = true)]
    public Protocol.Location[] Definition(Protocol.TextDocumentPositionParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Protocol.Location>();
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);
        if (unit == null) {
            return Array.Empty<Protocol.Location>();
        }

        // LSP-Position (Zeile/Zeichen) → Offset → Nav→Nav-Ziele (engine-seitig, VS-frei).
        var offset  = LspMapper.ToOffset(unit.Syntax.SyntaxTree.SourceText, param.Position);
        var targets = NavGoToService.GetGoToLocations(unit, offset);

        return targets.Select(LspMapper.ToLocation)
                      .OfType<Protocol.Location>()
                      .ToArray();
    }

    [JsonRpcMethod(Protocol.Methods.TextDocumentReferencesName, UseSingleObjectParameterDeserialization = true)]
    public async Task<Protocol.Location[]> References(Protocol.ReferenceParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Protocol.Location>();
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);
        if (unit == null) {
            return Array.Empty<Protocol.Location>();
        }

        var offset = LspMapper.ToOffset(unit.Syntax.SyntaxTree.SourceText, param.Position);
        var origin = NavReferenceService.FindSymbol(unit, offset);
        if (origin == null) {
            return Array.Empty<Protocol.Location>();
        }

        // Solution-weite Referenzsuche über die Engine-API; der Collector sammelt nur die Locations.
        var includeDeclaration = param.Context?.IncludeDeclaration ?? true;
        var collector          = new ReferenceCollector(includeDeclaration, CancellationToken.None);
        var args               = new FindReferencesArgs(origin, unit, _workspace.Solution, collector);

        await ReferenceFinder.FindReferencesAsync(args);

        return collector.Locations
                        .Select(LspMapper.ToLocation)
                        .OfType<Protocol.Location>()
                        .ToArray();
    }

    [JsonRpcMethod(Protocol.Methods.TextDocumentDocumentHighlightName, UseSingleObjectParameterDeserialization = true)]
    public Protocol.DocumentHighlight[] DocumentHighlight(Protocol.TextDocumentPositionParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Protocol.DocumentHighlight>();
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);
        if (unit == null) {
            return Array.Empty<Protocol.DocumentHighlight>();
        }

        var offset  = LspMapper.ToOffset(unit.Syntax.SyntaxTree.SourceText, param.Position);
        var symbols = NavReferenceService.GetHighlightSymbols(unit, offset);

        // documentHighlight ist per Definition dateilokal; das erste Symbol ist die Deklaration (Write).
        var normalizedFile = PathHelper.NormalizePath(filePath);
        var highlights     = new List<Protocol.DocumentHighlight>();

        for (var i = 0; i < symbols.Count; i++) {

            var location = symbols[i].Location;
            if (PathHelper.NormalizePath(location.FilePath) != normalizedFile) {
                continue;
            }

            highlights.Add(new Protocol.DocumentHighlight {
                Range = LspMapper.ToRange(location),
                Kind  = i == 0 ? Protocol.DocumentHighlightKind.Write : Protocol.DocumentHighlightKind.Read
            });
        }

        return highlights.ToArray();
    }

    [JsonRpcMethod(Protocol.Methods.TextDocumentRenameName, UseSingleObjectParameterDeserialization = true)]
    public Protocol.WorkspaceEdit? Rename(Protocol.RenameParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return null;
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);
        if (unit == null) {
            return null;
        }

        var sourceText = unit.Syntax.SyntaxTree.SourceText;
        var offset     = LspMapper.ToOffset(sourceText, param.Position);
        var settings   = EditorSettingsFor(sourceText);

        var renameFix = NavRenameService.GetRenameFix(unit, offset, settings);
        if (renameFix == null) {
            // Caret steht auf keinem umbenennbaren Symbol (Keyword, Whitespace, …).
            throw new LocalRpcException("You must rename an identifier.");
        }

        // Den neuen Namen prüfen (Identifier gültig? Name bereits vergeben?) und die Meldung als
        // JSON-RPC-Fehler melden — VS Code zeigt sie direkt am Rename-Eingabefeld an. (Eine clientseitige
        // Vorab-Validierung über prepareRename gibt es nicht: das LSP-Protokoll-Paket 17.2.8 kennt es nicht.)
        var newName           = param.NewName?.Trim() ?? string.Empty;
        var validationMessage = renameFix.ValidateSymbolName(newName);
        if (!string.IsNullOrEmpty(validationMessage)) {
            throw new LocalRpcException(validationMessage);
        }

        // Der Rename ist — wie in VS — dateilokal: alle TextChanges beziehen sich auf dieses Dokument.
        var edits = new List<Protocol.TextEdit>();
        foreach (var change in renameFix.GetTextChanges(newName)) {

            if (change.IsEmpty || change.Extent.End > sourceText.Length) {
                continue;
            }

            edits.Add(new Protocol.TextEdit {
                Range   = LspMapper.ToRange(sourceText.GetLocation(change.Extent)),
                NewText = change.ReplacementText
            });
        }

        if (edits.Count == 0) {
            // z.B. neuer Name == alter Name → nichts zu tun.
            return null;
        }

        return new Protocol.WorkspaceEdit {
            Changes = new Dictionary<string, Protocol.TextEdit[]> {
                // Exakt die vom Client gesendete URI-Form zurückspiegeln (file:///d%3A/… bleibt erhalten).
                [param.TextDocument.Uri.OriginalString] = edits.ToArray()
            }
        };
    }

    /// <summary>
    /// Editor-Einstellungen für die Refactoring-Engine. TabSize 4 (wie der VS-Default); der Zeilenumbruch
    /// wird aus dem Dokument erkannt (CRLF vs. LF), damit neu komponierte mehrzeilige Kanten zum Dokument passen.
    /// </summary>
    static TextEditorSettings EditorSettingsFor(SourceText sourceText) {
        var newLine = sourceText.Text.Contains("\r\n") ? "\r\n" : "\n";
        return new TextEditorSettings(tabSize: 4, newLine: newLine);
    }

    [JsonRpcMethod(Protocol.Methods.TextDocumentCodeActionName, UseSingleObjectParameterDeserialization = true)]
    public Protocol.CodeAction[] CodeAction(Protocol.CodeActionParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Protocol.CodeAction>();
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);
        if (unit == null) {
            return Array.Empty<Protocol.CodeAction>();
        }

        var sourceText = unit.Syntax.SyntaxTree.SourceText;
        var settings   = EditorSettingsFor(sourceText);

        // LSP-Range (Selektion oder reiner Caret) auf einen Engine-Offset-Bereich abbilden. Ein leerer
        // Bereich wird im Service auf den Token-Extent ausgedehnt, damit die Provider auch beim bloßen
        // Caret greifen.
        var start = LspMapper.ToOffset(sourceText, param.Range.Start);
        var end   = LspMapper.ToOffset(sourceText, param.Range.End);
        var range = TextExtent.FromBounds(Math.Min(start, end), Math.Max(start, end));

        var actions = NavCodeActionService.GetCodeActions(unit, range, settings, CancellationToken.None);

        var result    = new List<Protocol.CodeAction>();
        var seenTitles = new HashSet<string>();
        foreach (var action in actions) {

            // Doppelte Titel zusammenfassen (wie die VS-SuggestedActionsSource): es bleibt der erste Treffer.
            if (!seenTitles.Add(action.Title)) {
                continue;
            }

            var edit = ToWorkspaceEdit(param.TextDocument.Uri, sourceText, action.TextChanges);
            if (edit == null) {
                continue;
            }

            result.Add(new Protocol.CodeAction {
                Title = action.Title,
                Kind  = ToCodeActionKind(action.Category),
                Edit  = edit
            });
        }

        return result.ToArray();
    }

    /// <summary>
    /// Baut aus offset-basierten Engine-<see cref="TextChange"/> eine dateilokale <see cref="Protocol.WorkspaceEdit"/>
    /// gegen die exakt vom Client gesendete URI-Form (file:///d%3A/… bleibt erhalten). <c>null</c>, wenn nichts
    /// zu ändern ist.
    /// </summary>
    static Protocol.WorkspaceEdit? ToWorkspaceEdit(Uri uri, SourceText sourceText, IReadOnlyList<TextChange> changes) {

        var edits = new List<Protocol.TextEdit>();
        foreach (var change in changes) {

            if (change.IsEmpty || change.Extent.End > sourceText.Length) {
                continue;
            }

            edits.Add(new Protocol.TextEdit {
                Range   = LspMapper.ToRange(sourceText.GetLocation(change.Extent)),
                NewText = change.ReplacementText
            });
        }

        if (edits.Count == 0) {
            return null;
        }

        return new Protocol.WorkspaceEdit {
            Changes = new Dictionary<string, Protocol.TextEdit[]> {
                [uri.OriginalString] = edits.ToArray()
            }
        };
    }

    static Protocol.CodeActionKind ToCodeActionKind(CodeFixCategory category) => category switch {
        CodeFixCategory.Refactoring => Protocol.CodeActionKind.Refactor,
        _                           => Protocol.CodeActionKind.QuickFix
    };

    [JsonRpcMethod(Protocol.Methods.TextDocumentHoverName, UseSingleObjectParameterDeserialization = true)]
    public Protocol.Hover? Hover(Protocol.TextDocumentPositionParams param) {

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

        var hover = new Protocol.Hover {
            // Die Signatur als Nav-Codeblock; VS Code rendert sie monospace.
            Contents = new Protocol.MarkupContent {
                Kind  = Protocol.MarkupKind.Markdown,
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

    [JsonRpcMethod(Protocol.Methods.TextDocumentSemanticTokensFullName, UseSingleObjectParameterDeserialization = true)]
    public Protocol.SemanticTokens SemanticTokensFull(Protocol.SemanticTokensParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return new Protocol.SemanticTokens { Data = Array.Empty<int>() };
        }

        var syntaxTree = _workspace.GetSyntaxTree(filePath, CancellationToken.None);

        return new Protocol.SemanticTokens {
            Data = syntaxTree == null ? Array.Empty<int>() : SemanticTokensBuilder.Encode(syntaxTree)
        };
    }

    [JsonRpcMethod(Protocol.Methods.TextDocumentFoldingRangeName, UseSingleObjectParameterDeserialization = true)]
    public Protocol.FoldingRange[] FoldingRange(Protocol.FoldingRangeParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Protocol.FoldingRange>();
        }

        var syntaxTree = _workspace.GetSyntaxTree(filePath, CancellationToken.None);

        return syntaxTree == null
            ? Array.Empty<Protocol.FoldingRange>()
            : FoldingRangeBuilder.Build(syntaxTree);
    }

    [JsonRpcMethod(Protocol.Methods.TextDocumentCodeLensName, UseSingleObjectParameterDeserialization = true)]
    public Protocol.CodeLens[] CodeLens(Protocol.CodeLensParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Protocol.CodeLens>();
        }

        // Nur Positionen — rein syntaktisch und ohne Referenzsuche (die folgt träge in resolve).
        var syntaxTree = _workspace.GetSyntaxTree(filePath, CancellationToken.None);

        return syntaxTree == null
            ? Array.Empty<Protocol.CodeLens>()
            : CodeLensBuilder.Build(syntaxTree, param.TextDocument.Uri);
    }

    [JsonRpcMethod(Protocol.Methods.CodeLensResolveName, UseSingleObjectParameterDeserialization = true)]
    public async Task<Protocol.CodeLens> CodeLensResolve(Protocol.CodeLens codeLens) {

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
                                 .OfType<Protocol.Location>()
                                 .ToArray();

        var count = locations.Length;

        codeLens.Command = new Protocol.Command {
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

    [JsonRpcMethod("textDocument/prepareCallHierarchy", UseSingleObjectParameterDeserialization = true)]
    public CallHierarchyItem[] PrepareCallHierarchy(CallHierarchyPrepareParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<CallHierarchyItem>();
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);
        if (unit == null) {
            return Array.Empty<CallHierarchyItem>();
        }

        var offset = LspMapper.ToOffset(unit.Syntax.SyntaxTree.SourceText, param.Position);
        var task   = NavCallHierarchyService.PrepareCallHierarchy(unit, offset);
        if (task == null) {
            return Array.Empty<CallHierarchyItem>();
        }

        var item = CallHierarchyBuilder.FromDefinition(task);
        return item == null ? Array.Empty<CallHierarchyItem>() : new[] { item };
    }

    [JsonRpcMethod("callHierarchy/incomingCalls", UseSingleObjectParameterDeserialization = true)]
    public async Task<CallHierarchyIncomingCall[]> CallHierarchyIncomingCalls(CallHierarchyIncomingCallsParams param) {

        var task = ResolveCallHierarchyTask(param.Item);
        if (task == null) {
            return Array.Empty<CallHierarchyIncomingCall>();
        }

        var calls  = await NavCallHierarchyService.GetIncomingCallsAsync(task, _workspace.Solution, CancellationToken.None);
        var result = new List<CallHierarchyIncomingCall>();

        foreach (var call in calls) {

            var from = CallHierarchyBuilder.FromDefinition(call.Caller);
            if (from == null) {
                continue;
            }

            result.Add(new CallHierarchyIncomingCall {
                From       = from,
                FromRanges = call.CallSites.Select(LspMapper.ToRange).ToArray()
            });
        }

        return result.ToArray();
    }

    [JsonRpcMethod("callHierarchy/outgoingCalls", UseSingleObjectParameterDeserialization = true)]
    public CallHierarchyOutgoingCall[] CallHierarchyOutgoingCalls(CallHierarchyOutgoingCallsParams param) {

        var task = ResolveCallHierarchyTask(param.Item);
        if (task == null) {
            return Array.Empty<CallHierarchyOutgoingCall>();
        }

        var result = new List<CallHierarchyOutgoingCall>();

        foreach (var call in NavCallHierarchyService.GetOutgoingCalls(task)) {

            var to = CallHierarchyBuilder.FromDeclaration(call.Target);
            if (to == null) {
                continue;
            }

            result.Add(new CallHierarchyOutgoingCall {
                To         = to,
                FromRanges = call.CallSites.Select(LspMapper.ToRange).ToArray()
            });
        }

        return result.ToArray();
    }

    /// <summary>
    /// Findet die Task-Definition zu einem zurückgereichten <see cref="CallHierarchyItem"/> wieder:
    /// <see cref="CallHierarchyItem.Data"/> kommt als <c>JObject</c> ({Uri, Offset}) zurück (wie bei CodeLens),
    /// daraus Dokument laden und an der Bezeichner-Position die Task auflösen.
    /// </summary>
    ITaskDefinitionSymbol? ResolveCallHierarchyTask(CallHierarchyItem? item) {

        var data = (item?.Data as JObject)?.ToObject<CallHierarchyItemData>();
        if (data == null || string.IsNullOrEmpty(data.Uri)) {
            return null;
        }

        var filePath = NavUri.ToFilePath(new Uri(data.Uri));
        if (filePath == null) {
            return null;
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);
        return unit == null ? null : NavCallHierarchyService.PrepareCallHierarchy(unit, data.Offset);
    }

    [JsonRpcMethod(Protocol.Methods.TextDocumentCompletionName, UseSingleObjectParameterDeserialization = true)]
    public Protocol.CompletionItem[] Completion(Protocol.CompletionParams param) {

        var filePath = NavUri.ToFilePath(param.TextDocument.Uri);
        if (filePath == null) {
            return Array.Empty<Protocol.CompletionItem>();
        }

        var unit = _workspace.GetCodeGenerationUnit(filePath, CancellationToken.None);
        if (unit == null) {
            return Array.Empty<Protocol.CompletionItem>();
        }

        var sourceText = unit.Syntax.SyntaxTree.SourceText;
        var offset     = LspMapper.ToOffset(sourceText, param.Position);
        var items      = NavCompletionService.GetCompletions(unit, offset, _workspace.Solution);

        // SortText nach Index, damit die vom Service vorgegebene Reihenfolge (unverbundene Exits /
        // unreferenzierte Knoten zuerst, bzw. Elternverzeichnis → Verzeichnisse → Dateien) im Client
        // erhalten bleibt — sonst sortiert dieser alphabetisch.
        var result = new Protocol.CompletionItem[items.Count];
        for (var i = 0; i < items.Count; i++) {

            var item = items[i];

            var lspItem = new Protocol.CompletionItem {
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
                lspItem.TextEdit = new Protocol.TextEdit {
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

    static Protocol.CompletionItemKind ToCompletionItemKind(NavCompletionItemKind kind) => kind switch {
        NavCompletionItemKind.Keyword         => Protocol.CompletionItemKind.Keyword,
        NavCompletionItemKind.Task            => Protocol.CompletionItemKind.Class,
        NavCompletionItemKind.ConnectionPoint => Protocol.CompletionItemKind.Field,
        NavCompletionItemKind.Choice          => Protocol.CompletionItemKind.EnumMember,
        NavCompletionItemKind.GuiNode         => Protocol.CompletionItemKind.Interface,
        NavCompletionItemKind.File            => Protocol.CompletionItemKind.File,
        NavCompletionItemKind.Folder          => Protocol.CompletionItemKind.Folder,
        _                                     => Protocol.CompletionItemKind.Variable
    };

    [JsonRpcMethod(Protocol.Methods.ShutdownName)]
    public object? Shutdown() => null;

    [JsonRpcMethod(Protocol.Methods.ExitName)]
    public void Exit() {
        Environment.Exit(0);
    }

    Task UpdateOverlayAndPublishAsync(Uri uri, string text, bool publishDependents = false) {

        var normalizedPath = NavUri.ToNormalizedPath(uri);
        if (normalizedPath == null) {
            return Task.CompletedTask;
        }

        _workspace.OpenOrUpdate(normalizedPath, text);

        return publishDependents
            ? PublishDocumentAndDependentsAsync(uri)
            : PublishDocumentAsync(uri);
    }

    /// <summary>
    /// Publiziert die Diagnostics des Dokuments und anschließend die der (transitiv) inkludierenden Dateien.
    /// </summary>
    async Task PublishDocumentAndDependentsAsync(Uri uri) {

        await PublishDocumentAsync(uri);

        var filePath = NavUri.ToFilePath(uri);
        if (filePath == null) {
            return;
        }

        await _workspace.PublishDependentsAsync(filePath, PublishAsync, CancellationToken.None);
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

    Task PublishAsync(Uri uri, Protocol.Diagnostic[] diagnostics) {

        var publishParams = new Protocol.PublishDiagnosticParams {
            Uri         = uri,
            Diagnostics = diagnostics
        };

        return _rpc.NotifyWithParameterObjectAsync(Protocol.Methods.TextDocumentPublishDiagnosticsName, publishParams);
    }
}
