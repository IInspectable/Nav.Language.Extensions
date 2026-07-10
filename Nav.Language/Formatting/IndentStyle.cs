namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Der Einzugsstil des Formatters: ein Tab pro Einzugsstufe oder <see cref="NavFormattingOptions.IndentSize"/>
/// Spaces. Betrifft nur den <b>Einzug</b> am Zeilenanfang — Ausrichtungs-Padding innerhalb einer Zeile ist
/// immer Leerzeichen („Tabs für Einzug, Spaces für Ausrichtung").
/// </summary>
public enum IndentStyle {

    /// <summary>Ein Tab pro Einzugsstufe (Korpus-Mehrheit, Default).</summary>
    Tabs,

    /// <summary><see cref="NavFormattingOptions.IndentSize"/> Spaces pro Einzugsstufe.</summary>
    Spaces,

}
