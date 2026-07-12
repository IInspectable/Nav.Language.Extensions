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

    /// <summary>Die kanonischen Formatter-Defaults (Korpus-Mehrheit: Tabs, Breite 4, alle Ausrichtungen an, Leerzeilen-Deckel 3).</summary>
    public static readonly NavFormattingOptions Default = new();

    /// <summary>Der Einzugsstil (Tabs oder Spaces). Default: <see cref="Formatting.IndentStyle.Tabs"/> (Korpus-Mehrheit).</summary>
    public IndentStyle IndentStyle { get; init; } = IndentStyle.Tabs;

    /// <summary>Die Einzugsbreite in Zeichen — Spaces pro Einzugsstufe bzw. angenommene Tab-Breite. Default: 4.</summary>
    public int IndentSize { get; init; } = 4;

    /// <summary>Transitions-Pfeile einer Gruppe spaltenweise ausrichten. Default: <c>true</c>.</summary>
    public bool AlignArrows { get; init; } = true;

    /// <summary>
    /// Aufeinanderfolgende Fortsetzungs-Kanten (<c>--^</c>/<c>o-^</c>) einer Transitions-Gruppe
    /// spaltenweise ausrichten (tight unter dem längsten Ziel-Teil, in Quellreihenfolge zwischen Ziel-Teil
    /// und Trigger). Wie bei den Trailing-Kommentaren bricht bereits <b>eine einzelne</b> Leerzeile (bzw.
    /// eine eigene Kommentarzeile) den Block. Default: <c>true</c>.
    /// </summary>
    public bool AlignContinuations { get; init; } = true;

    /// <summary>
    /// Aufeinanderfolgende Trigger (<c>on …</c>/<c>spontaneous</c>) einer Transitions-Gruppe spaltenweise
    /// ausrichten (tight unter dem längsten Ziel-Teil). Wie bei den Trailing-Kommentaren bricht bereits
    /// <b>eine einzelne</b> Leerzeile (bzw. eine eigene Kommentarzeile) den Block. Default: <c>true</c>.
    /// </summary>
    public bool AlignTriggers { get; init; } = true;

    /// <summary>
    /// Aufeinanderfolgende <c>if</c>/<c>else if</c>/<c>else</c>-Bedingungen einer Transitions-Gruppe
    /// spaltenweise ausrichten (unter dem längsten Ziel-Teil). Wie bei den Trailing-Kommentaren bricht
    /// bereits <b>eine einzelne</b> Leerzeile (bzw. eine eigene Kommentarzeile) den Block. Default: <c>true</c>.
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
    /// Trailing-<c>//</c>-Kommentare am Zeilenende eines zusammenhängenden Anweisungs-Blocks an einer
    /// gemeinsamen Spalte ausrichten (tight — genau ein Space hinter der längsten Zeile der Gruppe).
    /// Anders als die übrigen Ausrichtungen bricht hier bereits <b>eine einzelne</b> Leerzeile (bzw. eine
    /// eigene Kommentarzeile) den Block. Default: <c>true</c>.
    /// </summary>
    public bool AlignTrailingComments { get; init; } = true;

    /// <summary>
    /// Wie die Zielspalte einer Ausrichtungsgruppe aus den kanonischen Zeilenbreiten folgt.
    /// Default: <see cref="Formatting.AlignmentColumnPolicy.NextTabStop"/>.
    /// Ausrichtungs-Padding ist immer Leerzeichen, nie Tabs — unabhängig vom <see cref="IndentStyle"/>.
    /// </summary>
    public AlignmentColumnPolicy AlignmentColumnPolicy { get; init; } = AlignmentColumnPolicy.NextTabStop;

    /// <summary>
    /// Ob hinter dem letzten Inhalt eine abschließende Newline ergänzt wird. Default: <c>true</c>.
    /// </summary>
    /// <remarks>
    /// Steuert <b>nur</b> die abschließende Newline. Der Final-Gap wird immer normalisiert — EOF-Trailing-Trim
    /// (Leerzeilen hinter dem letzten Inhalt entfallen) und die Normalisierung von Kommentar-/Direktivzeilen am
    /// Dateiende greifen unabhängig von dieser Option; der Skiped-Guard (BOM etc.) bleibt davor unberührt.
    /// Trailing-Whitespace-Trim ist kein eigener Schalter: der Gap-Rewriter schreibt jede angefasste Lücke
    /// kanonisch neu, das Strippen ist ein bedingungsloses Nebenprodukt dieses Modells.
    /// </remarks>
    public bool InsertFinalNewline { get; init; } = true;

    /// <summary>
    /// Deckel für aufeinanderfolgende Leerzeilen: Läufe von mehr als so vielen Leerzeilen werden beim
    /// Formatieren darauf gekappt (Kommentar-/Direktivzeilen zählen nicht als Leerzeile und setzen den
    /// Lauf zurück). <c>3</c> (Default) = bis zu drei Leerzeilen am Stück bleiben stehen, alles darüber
    /// wird auf drei gekappt; <c>null</c> = <b>kein</b> Deckel — die vom Autor gesetzte vertikale Trennung
    /// bleibt vollständig erhalten. Gilt gleichermaßen mitten im Code, am Dateianfang und am Dateiende.
    /// </summary>
    /// <remarks>
    /// <b>Nur ≥ 2 ist zulässig</b> (2, 3, 4, … — nicht 0/1). Der Deckel darf einen Leerzeilen-Lauf nie
    /// <b>unter</b> die Gruppenbruch-Schwelle der Spaltenausrichtung drücken: eine Ausrichtungsgruppe
    /// (Pfeile/Node-Raster) bricht bei <b>zwei</b> Leerzeilen (<c>interruptLines ≥ 2</c>). Ein Deckel bei 1
    /// kappte einen 2-Leerzeilen-Lauf (= bewusster Gruppenbruch) auf eine Leerzeile (= kein Bruch) — vorher
    /// getrennte Transitionen würden dann <b>zusammengruppiert</b> und ausgerichtet, und das erst im
    /// <b>zweiten</b> Lauf (die Gruppen werden aus dem geparsten Baum gelesen, vor dem Kappen) → nicht mehr
    /// idempotent. <b>Deshalb</b> ist 1 verboten.
    /// <para>Jeder Deckel <b>≥ 2</b> ist dagegen unkritisch: er lässt jeden ≥ 2-Lauf ≥ 2 (Bruch bleibt Bruch)
    /// und jeden 1-Lauf unberührt — die Gruppierung ändert sich nie, egal ob 2, 3 oder mehr. <b>2 ist also
    /// nur der Boden</b> (die Bruch-Schwelle selbst); höhere Werte lassen bloß mehr vertikale Luft stehen.
    /// Werte &lt; 2 werden still auf 2 geklemmt; <c>null</c> schaltet den Deckel ganz ab. Idempotent, weil
    /// kein Lauf die Schwelle überquert → ein zweiter Lauf klassifiziert identisch.</para>
    /// <para><b>Warum 3 als Default:</b> im realen <c>.nav</c>-Bestand (Korpus, 1913 Dateien, 35 530
    /// Leerzeilen-Läufe) ist 1 Leerzeile die Norm (87 %), 2 der Abschnitts-Trenner (11 %), 3 der starke
    /// Trenner (1,4 %); ab 4 wird es Rauschen. Ein Deckel 3 lässt damit 99,6 % aller Läufe unberührt und
    /// kappt nur die ~0,4 % offensichtlich versehentlich zu großen Lücken (4 … 8 Leerzeilen) — er respektiert
    /// das Trenner-Vokabular des Bestands und entfernt bloß die Ausreißer.</para>
    /// </remarks>
    public int? MaxBlankLines {
        get => _maxBlankLines;
        init => _maxBlankLines = value is { } v ? System.Math.Max(2, v) : null;
    }

    readonly int? _maxBlankLines = 3;

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
