#region Using Directives

using Microsoft.VisualStudio.Text.Tagging;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.HighlightReferences; 

/// <summary>
/// Der <see cref="TextMarkerTag"/> für eine hervorgehobene Referenz des Symbols unter dem Cursor.
/// Der <see cref="ReferenceHighlightTagger"/> hängt je Fundstelle einen solchen Tag an den zugehörigen
/// <see cref="Microsoft.VisualStudio.Text.SnapshotSpan"/>; das gebundene Markierungsformat
/// (<see cref="MarkerFormatDefinitionNames.ReferenceHighlight"/>) bestimmt die Optik. Die abgesetzte
/// Definition nutzt die abgeleitete Variante <see cref="DefinitionHighlightTag"/>.
/// </summary>
class ReferenceHighlightTag : TextMarkerTag {
        
    /// <summary>Erzeugt den Tag mit dem Referenz-Markierungsformat <see cref="MarkerFormatDefinitionNames.ReferenceHighlight"/>.</summary>
    public ReferenceHighlightTag() : base(MarkerFormatDefinitionNames.ReferenceHighlight) {

    }

    /// <summary>
    /// Erzeugt den Tag mit einem abweichenden Markierungsformat <paramref name="type"/>; genutzt von der
    /// abgeleiteten <see cref="DefinitionHighlightTag"/> für die abgesetzte Definition.
    /// </summary>
    protected ReferenceHighlightTag(string type) : base(type) {

    }
}
