namespace Pharmatechnik.Nav.Language.Extension.HighlightReferences; 

/// <summary>
/// Die Namensschlüssel der beiden Markierungsformate (<c>MarkerFormatDefinition</c>) des
/// Referenz-Highlightings. Ein <see cref="Microsoft.VisualStudio.Text.Tagging.TextMarkerTag"/>
/// nennt über einen dieser Schlüssel das VS-Textmarker-Format, das seine Optik (Hintergrund/Rahmen)
/// festlegt — hier eines für die hervorgehobenen Referenzen und ein zweites, abgesetztes für die
/// Definition.
/// </summary>
static class MarkerFormatDefinitionNames {

    /// <summary>Formatschlüssel für die hervorgehobenen Referenzen (<see cref="ReferenceHighlightTag"/>).</summary>
    public const string ReferenceHighlight  = "MarkerFormatDefinition/HighlightedReference";
    /// <summary>Formatschlüssel für die abgesetzte Definition (<see cref="DefinitionHighlightTag"/>).</summary>
    public const string DefinitionHighlight = "MarkerFormatDefinition/HighlightedDefinition";

}
