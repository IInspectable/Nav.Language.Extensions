namespace Pharmatechnik.Nav.Language.Extension.HighlightReferences; 

/// <summary>
/// Der Textmarker-Tag für die <b>Definition</b> unter den hervorgehobenen Referenzen: die erste,
/// als „Definition" geltende Fundstelle (siehe <see cref="ReferenceFinder"/>) erhält dieses Tag,
/// alle weiteren das schlichtere <see cref="ReferenceHighlightTag"/>. Bindet an das eigene
/// Markierungsformat <see cref="MarkerFormatDefinitionNames.DefinitionHighlight"/>, sodass die
/// Definition optisch von ihren Referenzen abgesetzt werden kann.
/// </summary>
class DefinitionHighlightTag : ReferenceHighlightTag {

    /// <summary>
    /// Erzeugt den Tag und bindet ihn an das Markierungsformat
    /// <see cref="MarkerFormatDefinitionNames.DefinitionHighlight"/>.
    /// </summary>
    public DefinitionHighlightTag() : base(MarkerFormatDefinitionNames.DefinitionHighlight) {

    }
}
