namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Steuert den Nav-Code-Formatter: Einzugsstil, Ausrichtungs-Features und Hygiene-Regeln.
/// Kanonische Grundeinstellung ist <see cref="Default"/> — die einzige Autorität für die
/// Formatter-Defaults aller Hosts (analog <see cref="Completion.NavCompletionService.TriggerCharacters"/>);
/// abweichende Konfigurationen werden per <c>with</c>-Ausdruck abgeleitet.
/// </summary>
/// <remarks>
/// <see cref="IndentStyle"/>/<see cref="IndentSize"/> kommen aus dem bestehenden Editor-Konfig-Kanal des
/// jeweiligen Hosts (VS <c>textView.Options</c>, LSP <c>FormattingOptions</c>, CLI-Flag) und sind hier nur
/// der neutrale Transport; <see cref="Text.TextEditorSettings"/> wird bewusst nicht erweitert.
/// </remarks>
public record NavFormattingOptions {

    /// <summary>Die kanonischen Formatter-Defaults (Korpus-Mehrheit: Tabs, Breite 4, alle Ausrichtungen an).</summary>
    public static readonly NavFormattingOptions Default = new();

    /// <summary>Der Einzugsstil (Tabs oder Spaces). Default: <see cref="Formatting.IndentStyle.Tabs"/> (Korpus-Mehrheit).</summary>
    public IndentStyle IndentStyle { get; init; } = IndentStyle.Tabs;

    /// <summary>Die Einzugsbreite in Zeichen — Spaces pro Einzugsstufe bzw. angenommene Tab-Breite. Default: 4.</summary>
    public int IndentSize { get; init; } = 4;

    /// <summary>Transitions-Pfeile einer Gruppe spaltenweise ausrichten. Default: <c>true</c>.</summary>
    public bool AlignArrows { get; init; } = true;

    /// <summary>
    /// Aufeinanderfolgende <c>if</c>/<c>else if</c>/<c>else</c>-Bedingungen einer Transitions-Gruppe
    /// spaltenweise ausrichten (unter dem längsten Ziel-Teil). Default: <c>true</c>.
    /// </summary>
    public bool AlignConditions { get; init; } = true;

    /// <summary>
    /// Node-Deklarationen auf das 3-Spalten-Raster <c>keyword | node | rest</c> ausrichten.
    /// Default: <c>true</c>.
    /// </summary>
    public bool AlignNodeGrid { get; init; } = true;

    /// <summary>
    /// Task-Kopf-Code-Blöcke stapeln (Block 1 inline hinter dem Identifier, weitere je Zeile linksbündig
    /// darunter) und mehrzeilige <c>[params]</c> unter dem ersten Parameter ausrichten. Default: <c>true</c>.
    /// </summary>
    public bool AlignTaskHeadBlocks { get; init; } = true;

    /// <summary>
    /// Wie die Zielspalte einer Ausrichtungsgruppe aus den kanonischen Zeilenbreiten folgt.
    /// Default: <see cref="Formatting.AlignmentColumnPolicy.NextTabStop"/>.
    /// Ausrichtungs-Padding ist immer Leerzeichen, nie Tabs — unabhängig vom <see cref="IndentStyle"/>.
    /// </summary>
    public AlignmentColumnPolicy AlignmentColumnPolicy { get; init; } = AlignmentColumnPolicy.NextTabStop;

    /// <summary>Am Dateiende genau eine abschließende Newline sicherstellen. Default: <c>true</c>.</summary>
    public bool InsertFinalNewline { get; init; } = true;

    /// <summary>Whitespace am Zeilenende entfernen (auch auf Leerzeilen). Default: <c>true</c>.</summary>
    public bool TrimTrailingWhitespace { get; init; } = true;

    /// <summary>
    /// Ob der interne Achse-A-Wächter nach dem Formatieren mitläuft (Re-Parse des Ergebnisses + Vergleich des
    /// signifikanten Token-Stroms, der Direktiven und der Diagnostics). Default: <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Bewusst per Default aus — der Wächter re-parst und wendet die Changes ein zweites Mal an (grob eine
    /// Verdopplung von Parse + Apply). Er ist ein reiner <b>Entwicklungs-Selbsttest</b>: schlägt er an, ist
    /// das immer ein Formatter-Bug, kein legitimer Laufzeitzustand. Da die Hosts einen Debug-Build ausliefern,
    /// darf er auch dort nicht mitlaufen; ausschließlich die Tests schalten ihn per Opt-in ein.
    /// </remarks>
    public bool VerifyResult { get; init; } = false;

}
