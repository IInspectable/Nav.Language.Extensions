namespace Pharmatechnik.Nav.Language.Completion;

/// <summary>
/// Neutrale (VS-freie) Beschreibung eines Vervollständigungs-Vorschlags. Der LSP-Server bildet
/// <see cref="Kind"/> auf eine LSP-<c>CompletionItemKind</c> ab und erhält die von
/// <see cref="NavCompletionService"/> vorgegebene Reihenfolge über ein index-basiertes <c>SortText</c>.
/// </summary>
public enum NavCompletionItemKind {
    Keyword,
    Task,
    ConnectionPoint,
    Choice,
    GuiNode,
    Node
}

/// <summary>Ein einzelner Vervollständigungs-Vorschlag (Anzeigetext + Kategorie).</summary>
public sealed class NavCompletionItem {

    public NavCompletionItem(string label, NavCompletionItemKind kind) {
        Label = label;
        Kind  = kind;
    }

    /// <summary>Der einzufügende/angezeigte Text (Symbol- bzw. Keyword-Name).</summary>
    public string Label { get; }

    /// <summary>Die Kategorie des Vorschlags — bestimmt das Icon im Client.</summary>
    public NavCompletionItemKind Kind { get; }
}
