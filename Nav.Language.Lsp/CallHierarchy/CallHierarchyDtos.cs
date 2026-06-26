#region Using Directives

using System;

using Newtonsoft.Json;

using Protocol = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Lsp;

// Das Protokoll-Paket 17.2.8 kennt KEINE Call-Hierarchy-DTOs (verifiziert; wie schon prepareRename).
// Daher hier eigene, minimale DTOs. Sie gehen über denselben Newtonsoft-Formatter wie die Paket-DTOs;
// der globale CamelCase-Resolver (Program.cs) bildet die PascalCase-Properties auf die LSP-Feldnamen ab.
// Wiederverwendet werden die vorhandenen Paket-Typen TextDocumentIdentifier/Position/Range/SymbolKind.

/// <summary>Parameter für <c>textDocument/prepareCallHierarchy</c>.</summary>
sealed class CallHierarchyPrepareParams {
    public Protocol.TextDocumentIdentifier TextDocument { get; set; } = null!;
    public Protocol.Position               Position     { get; set; } = null!;
}

/// <summary>Parameter für <c>callHierarchy/incomingCalls</c>.</summary>
sealed class CallHierarchyIncomingCallsParams {
    public CallHierarchyItem Item { get; set; } = null!;
}

/// <summary>Parameter für <c>callHierarchy/outgoingCalls</c>.</summary>
sealed class CallHierarchyOutgoingCallsParams {
    public CallHierarchyItem Item { get; set; } = null!;
}

/// <summary>
/// Ein Knoten der Aufrufhierarchie (eine Nav-Task). <see cref="Data"/> trägt — wie bei CodeLens — die
/// Wiederfindungs-Info ({Uri, Offset}) durch den Round-Trip und kommt eingehend als <c>JObject</c> zurück.
/// </summary>
sealed class CallHierarchyItem {
    public string              Name           { get; set; } = "";
    public Protocol.SymbolKind Kind           { get; set; }

    // WICHTIG: ohne diesen Konverter serialisiert Newtonsoft System.Uri als OriginalString — bei einer aus
    // einem Windows-Pfad gebauten Uri also "D:\...\x.nav" (Backslashes), KEINE file://-URI. Der VS-Code-Client
    // (vscode-languageclient) parst die Item-Uri per Uri.parse: "D:\..." wird als Schema "d" fehlinterpretiert,
    // die "from"/"to"-Knoten erhalten eine kaputte Uri und werden beim Rendern verworfen (leere Aufrufliste).
    // Derselbe DocumentUriConverter, den auch Protocol.Location.Uri trägt, schreibt die korrekte file://-Form.
    [JsonConverter(typeof(Protocol.DocumentUriConverter))]
    public Uri                 Uri            { get; set; } = null!;
    public Protocol.Range      Range          { get; set; } = null!;
    public Protocol.Range      SelectionRange { get; set; } = null!;
    public string?             Detail         { get; set; }
    public object?             Data           { get; set; }
}

/// <summary>Ein eingehender Aufruf: der Aufrufer (<see cref="From"/>) und seine Aufrufstellen.</summary>
sealed class CallHierarchyIncomingCall {
    public CallHierarchyItem From       { get; set; } = null!;
    public Protocol.Range[]  FromRanges { get; set; } = Array.Empty<Protocol.Range>();
}

/// <summary>Ein ausgehender Aufruf: das Ziel (<see cref="To"/>) und die Aufrufstellen im Aufrufer.</summary>
sealed class CallHierarchyOutgoingCall {
    public CallHierarchyItem To         { get; set; } = null!;
    public Protocol.Range[]  FromRanges { get; set; } = Array.Empty<Protocol.Range>();
}

/// <summary>
/// Durch den Round-Trip getragene Wiederfindungs-Info eines <see cref="CallHierarchyItem"/> — exakt das
/// <c>CodeLensData</c>-Muster: Dokument-URI + Offset des Task-Bezeichners.
/// </summary>
sealed class CallHierarchyItemData {
    public string Uri    { get; set; } = "";
    public int    Offset { get; set; }
}
