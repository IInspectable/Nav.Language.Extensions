#region Using Directives

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Diagnostics;

// View-skopiert (nicht buffer-skopiert) und auf die "Editable"-Rolle gefiltert: So entstehen
// Diagnose-Squiggles ausschließlich im echten, editierbaren Dokument-Editor. Read-only-Ansichten
// wie Annotate/Blame, Diff/Vergleich und History tragen diese Rolle nicht — dort ist der Buffer nur
// ein Snapshot einer Revision ohne Solution-Kontext, sodass die Include-Auflösung scheitern und eine
// Lawine kontextloser Semantikfehler erzeugen würde.
[Export(typeof(IViewTaggerProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[TextViewRole(PredefinedTextViewRoles.Editable)]
[TagType(typeof(DiagnosticErrorTag))]
sealed class DiagnosticErrorTaggerProvider : IViewTaggerProvider {
    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {

        // Nur am Top-Level-Buffer der Dokument-Ansicht taggen.
        if (textView?.TextBuffer != buffer) {
            return null;
        }

        return DiagnosticErrorTagger.Create<T>(buffer);
    }
}