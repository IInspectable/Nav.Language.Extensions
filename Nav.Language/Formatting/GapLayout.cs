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

    }

    /// <summary>Spaces bis zur vorberechneten Gruppenspalte (siehe <see cref="AlignmentMap"/>).</summary>
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
    public sealed record NewLineAlignedColumn(int BlankLinesBefore, ColumnId Column): GapLayout;

    /// <summary>Lücke unverändert lassen (unterdrückte Region) — es wird kein Change emittiert.</summary>
    public sealed record Verbatim: GapLayout {

        public static readonly Verbatim Instance = new();

    }

}