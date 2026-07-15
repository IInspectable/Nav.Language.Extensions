using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language.Completion;

/// <summary>
/// Neutrale (VS-freie) Beschreibung eines Vervollständigungs-Vorschlags. Der LSP-Server bildet
/// <see cref="NavCompletionItem.Kind"/> auf eine LSP-<c>CompletionItemKind</c> ab und erhält die von
/// <see cref="NavCompletionService"/> vorgegebene Reihenfolge über ein index-basiertes <c>SortText</c>.
/// </summary>
public enum NavCompletionItemKind {

    /// <summary>Ein Sprach-Schlüsselwort oder Edge-Operator (<c>task</c>, <c>on</c>, <c>--&gt;</c> …).</summary>
    Keyword,
    /// <summary>Ein Task — eine Task-Deklaration (<c>taskref</c>) oder ein Task-Knoten.</summary>
    Task,
    /// <summary>Ein Connection-Point (<c>init</c>/<c>exit</c>/<c>end</c>).</summary>
    ConnectionPoint,
    /// <summary>Ein Choice-Knoten.</summary>
    Choice,
    /// <summary>Ein GUI-Knoten (<c>view</c>/<c>dialog</c>).</summary>
    GuiNode,
    /// <summary>Ein sonstiger Knoten ohne spezifischere Kategorie.</summary>
    Node,
    /// <summary>Eine Datei — Ergebnis der Pfad-Vervollständigung in <c>taskref "…"</c>.</summary>
    File,
    /// <summary>Ein Verzeichnis — Ergebnis der Pfad-Vervollständigung in <c>taskref "…"</c>.</summary>
    Folder

}

/// <summary>Ein einzelner Vervollständigungs-Vorschlag (Anzeigetext + Kategorie + optional Einfügetext/Ersetzungsbereich).</summary>
public sealed class NavCompletionItem {

    /// <summary>Erzeugt einen Vorschlag.</summary>
    /// <param name="label">Der angezeigte Text (<see cref="Label"/>).</param>
    /// <param name="kind">Die Kategorie (<see cref="Kind"/>) — bestimmt das Icon im Client.</param>
    /// <param name="insertText">Der einzufügende Text (<see cref="InsertText"/>); <c>null</c> übernimmt <paramref name="label"/>.</param>
    /// <param name="replacementExtent">Der zu ersetzende Bereich (<see cref="ReplacementExtent"/>); <c>null</c>, wenn der Client ihn selbst bestimmt.</param>
    /// <param name="detail">Optionaler Zusatztext (<see cref="Detail"/>).</param>
    /// <param name="symbol">Das zugrunde liegende Symbol bei symbolbasierten Vorschlägen (<see cref="Symbol"/>); sonst <c>null</c>.</param>
    /// <param name="description">Optionale Erläuterung fürs Doku-Panel (<see cref="Description"/>).</param>
    public NavCompletionItem(string label, NavCompletionItemKind kind, string? insertText = null, TextExtent? replacementExtent = null, string? detail = null, ISymbol? symbol = null, string? description = null) {
        Label             = label;
        Kind              = kind;
        InsertText        = insertText ?? label;
        ReplacementExtent = replacementExtent;
        Detail            = detail;
        Symbol            = symbol;
        Description        = description;
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
    /// Optionale Erläuterung des Vorschlags (das ausführliche Doku-Panel des Clients) — bei
    /// Keyword-Vorschlägen (inkl. der Edge-Operatoren) die Bedeutung des Keywords
    /// (<see cref="SyntaxFacts.GetKeywordDescription"/>). <c>null</c>, wenn es keine gibt (Namen, Pfade).
    /// </summary>
    public string? Description { get; }

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

    /// <summary>
    /// Eine Kopie dieses Vorschlags mit gesetztem <see cref="ReplacementExtent"/> (alle übrigen Werte
    /// unverändert). Trägt den zentral bestimmten Operator-Ersetzungsbereich nachträglich an — siehe
    /// <c>NavCompletionService.WithOperatorReplacements</c>.
    /// </summary>
    public NavCompletionItem WithReplacementExtent(TextExtent extent) {
        return new NavCompletionItem(Label, Kind, InsertText, extent, Detail, Symbol, Description);
    }

}