namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Was zwischen zwei aufeinanderfolgenden signifikanten Token stehen soll — das geschlossene
/// Layout-Vokabular der Regeln. Eine Regel liefert direkt eine dieser Entscheidungen; es gibt keinen
/// Solver und keine Fernwirkung. Der <see cref="GapRenderer"/> ist die einzige Stelle, die ein Layout
/// zusammen mit der zu erhaltenden Trivia (Kommentare, Direktiven) zu einem String macht.
/// </summary>
abstract record GapLayout {

    GapLayout() {
    }

    /// <summary>Kein Whitespace — die Token kleben aneinander (z.B. <c>Node:Port</c>).</summary>
    public sealed record Nothing: GapLayout {

        public static readonly Nothing Instance = new();

    }

    /// <summary>Genau ein Space.</summary>
    public sealed record SingleSpace: GapLayout {

        public static readonly SingleSpace Instance = new();

        /// <summary>
        /// Die Pull-up-Variante der Task-/<c>taskref</c>-Kopf-Kanonisierung: bloße authored Newlines in
        /// der Lücke werden hochgezogen (Schranken-Ausnahme im Renderer) — zeilen-erzwingende Trivia
        /// (<c>//</c>-Kommentar, mehrzeiliger Block-Kommentar, Direktive) bleibt Sache der
        /// Renderer-Schranke und verhindert das Hochziehen weiterhin.
        /// </summary>
        public static readonly SingleSpace PullUp = new() { PullUpAuthoredLineBreaks = true };

        /// <summary>Ob bloße authored Newlines hochgezogen werden dürfen (siehe <see cref="PullUp"/>).</summary>
        public bool PullUpAuthoredLineBreaks { get; init; }

    }

    /// <summary>Spaces bis zur vorberechneten Gruppenspalte (siehe <see cref="AlignmentMap"/>).</summary>
    /// <param name="Column">
    /// Benennt <b>welche</b> Spalte gemeint ist — reine Selbstdokumentation im Regel-Code. Der
    /// <see cref="GapRenderer"/> wertet den Wert nicht aus, sondern schlägt die aufgelöste Space-Zahl
    /// allein über <c>Extent.Start</c> in der <see cref="AlignmentMap"/> nach (der Vorpass hat die
    /// spaltenspezifische Logik bereits erledigt).
    /// </param>
    public sealed record AlignedColumn(ColumnId Column): GapLayout;

    /// <summary>
    /// Zeilenumbruch vor dem nächsten Token auf Einzugstiefe <paramref name="IndentDepth"/>.
    /// <paramref name="BlankLinesBefore"/> ist die Autorenzahl der Leerzeilen davor — Leerzeilen werden
    /// nie kollabiert, eine Regel darf das Minimum aber anheben.
    /// </summary>
    public sealed record NewLine(int BlankLinesBefore, int IndentDepth): GapLayout;

    /// <summary>
    /// Zeilenumbruch, dann Spaces bis zur Gruppenspalte statt Tiefen-Einzug — für den
    /// Task-Kopf-Block-Stapel und mehrzeiliges <c>[params]</c>.
    /// </summary>
    /// <param name="BlankLinesBefore">
    /// Die Autorenzahl der Leerzeilen vor dem Umbruch als Minimum — wie bei <see cref="NewLine"/>. Der
    /// kanonische Kopf-Stapel reicht hier stets <c>0</c> herein und kollabiert die authored Leerzeilen
    /// zwischen den gestapelten Blöcken (Deckel <c>0</c> im <see cref="GapRenderer"/>).
    /// </param>
    /// <param name="Column">
    /// Benennt <b>welche</b> Spalte gemeint ist — reine Selbstdokumentation im Regel-Code; der
    /// <see cref="GapRenderer"/> schlägt die absolute Spalte nach dem Umbruch allein über
    /// <c>Extent.Start</c> in der <see cref="AlignmentMap"/> nach (vgl. <see cref="AlignedColumn"/>).
    /// </param>
    public sealed record NewLineAlignedColumn(int BlankLinesBefore, ColumnId Column): GapLayout;

    /// <summary>Lücke unverändert lassen (unterdrückte Region) — es wird kein Change emittiert.</summary>
    public sealed record Verbatim: GapLayout {

        public static readonly Verbatim Instance = new();

    }

}