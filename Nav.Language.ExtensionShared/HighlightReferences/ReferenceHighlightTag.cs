#region Using Directives

using Microsoft.VisualStudio.Text.Tagging;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.HighlightReferences; 

class ReferenceHighlightTag : TextMarkerTag {
        
    public ReferenceHighlightTag() : base(MarkerFormatDefinitionNames.ReferenceHighlight) {

    }

    protected ReferenceHighlightTag(string type) : base(type) {

    }
}