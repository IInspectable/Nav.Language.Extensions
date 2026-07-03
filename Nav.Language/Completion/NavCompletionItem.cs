using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language.Completion;

/// <summary>
/// Neutrale (VS-freie) Beschreibung eines Vervollständigungs-Vorschlags. Der LSP-Server bildet
/// <see cref="NavCompletionItem.Kind"/> auf eine LSP-<c>CompletionItemKind</c> ab und erhält die von
/// <see cref="NavCompletionService"/> vorgegebene Reihenfolge über ein index-basiertes <c>SortText</c>.
/// </summary>
public enum NavCompletionItemKind {

    Keyword,
    Task,
    ConnectionPoint,
    Choice,
    GuiNode,
    Node,
    File,
    Folder

}

/// <summary>Ein einzelner Vervollständigungs-Vorschlag (Anzeigetext + Kategorie + optional Einfügetext/Ersetzungsbereich).</summary>
public sealed class NavCompletionItem {

    public NavCompletionItem(string label, NavCompletionItemKind kind, string? insertText = null, TextExtent? replacementExtent = null, string? detail = null, ISymbol? symbol = null) {
        Label             = label;
        Kind              = kind;
        InsertText        = insertText ?? label;
        ReplacementExtent = replacementExtent;
        Detail            = detail;
        Symbol            = symbol;
    }

    /// <summary>Der angezeigte Text (Symbol-/Keyword-Name bzw. Datei-/Verzeichnisname).</summary>
    public string Label { get; }

    /// <summary>Die Kategorie des Vorschlags — bestimmt das Icon im Client.</summary>
    public NavCompletionItemKind Kind { get; }

    /// <summary>Der einzufügende Text — weicht bei Pfad-Vorschlägen vom <see cref="Label"/> ab (relativer Pfad).</summary>
    public string InsertText { get; }

    /// <summary>Optionaler Zusatztext (vom Client rechts/grau dargestellt) — bei Pfad-Vorschlägen der relative Pfad.</summary>
    public string? Detail { get; }

    /// <summary>
    /// Der zu ersetzende Bereich (absolute Dokument-Offsets) — gesetzt bei Pfad-Vorschlägen, damit der
    /// Client den gesamten Inhalt zwischen den Anführungszeichen ersetzt (statt nur das aktuelle Wort).
    /// <c>null</c>, wenn der Client den Ersetzungsbereich selbst bestimmen soll.
    /// </summary>
    public TextExtent? ReplacementExtent { get; }

    /// <summary>
    /// Das zugrunde liegende Symbol bei symbolbasierten Vorschlägen (Knoten, Task-Deklaration,
    /// Connection-Point) — sonst <c>null</c> (Keyword-/Pfad-Vorschläge). Hosts mit reicher Darstellung
    /// (VS) bauen daraus Icon und QuickInfo-Tooltip; der LSP-Server ignoriert es.
    /// </summary>
    public ISymbol? Symbol { get; }

}