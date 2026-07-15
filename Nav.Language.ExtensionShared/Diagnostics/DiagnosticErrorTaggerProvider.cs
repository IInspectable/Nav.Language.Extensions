#region Using Directives

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Diagnostics;

/// <summary>
/// MEF-Provider, der für <c>.nav</c>-TextBuffer einen <see cref="DiagnosticErrorTagger"/> bereitstellt
/// (Tag-Typ <see cref="DiagnosticErrorTag"/>) und ihn so in die Fehler-Tagging-Infrastruktur von Visual
/// Studio einklinkt.
/// </summary>
[Export(typeof(ITaggerProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[TagType(typeof(DiagnosticErrorTag))]
sealed class DiagnosticErrorTaggerProvider : ITaggerProvider {
    /// <summary>Erzeugt einen <see cref="DiagnosticErrorTagger"/> für den angegebenen <paramref name="buffer"/>.</summary>
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
        return DiagnosticErrorTagger.Create<T>(buffer);
    }
}
