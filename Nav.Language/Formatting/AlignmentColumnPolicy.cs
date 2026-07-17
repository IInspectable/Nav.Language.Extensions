namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Wie die Zielspalte einer Ausrichtungsgruppe aus den kanonischen Zeilenbreiten folgt. Boden aller
/// Policies ist <c>tightMin = max(kanonische Breite) + 1</c> — weniger als ein Space ist nie möglich;
/// Ausreißer, deren natürliche Breite die Zielspalte erreicht, bekommen ein Space und überlaufen.
/// </summary>
public enum AlignmentColumnPolicy {

    /// <summary>
    /// Zielspalte = nächster Tab-Stopp (Vielfaches von <see cref="NavFormattingOptions.IndentSize"/>)
    /// ≥ <c>tightMin</c>. Ehrt die im Korpus beobachtete Autoren-Absicht (Tab-Stopp-Artefakte),
    /// deterministisch und ausreißer-immun. Default.
    /// </summary>
    NextTabStop,

    /// <summary>Zielspalte = <c>tightMin</c> — die reinste kanonische Form (genau ein Space hinter der breitesten Zeile).</summary>
    Tight,

    /// <summary>
    /// Zielspalte = <c>max(tightMin, dominante Ist-Spalte)</c> — bewahrt bewusst breitere, konsistente
    /// Autorenspalten. Liest als einzige Policy den Ist-Whitespace (braucht Tab-Auflösung) und ist
    /// deshalb nicht Default.
    /// </summary>
    PreserveDominant,

}
