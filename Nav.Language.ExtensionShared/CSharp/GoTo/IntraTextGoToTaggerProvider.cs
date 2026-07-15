#region Using Directives

using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CSharp.GoTo; 

/// <summary>
/// MEF-Provider (<see cref="ITaggerProvider"/>), der in C#-Puffern die Daten-Tagger-Schicht der
/// Nav-GoTo-Adornments bereitstellt: Er erzeugt je <see cref="ITextBuffer"/> genau einen
/// <see cref="IntraTextGoToTagger"/> (Singleton über die Buffer-Properties), der aus den
/// Nav-Annotationen des generierten C#-Codes <see cref="IntraTextGoToTag"/>s berechnet. Die daraus
/// sichtbaren Adornments fügt darüber der view-gebundene <see cref="IntraTextGoToAdornmentTaggerProvider"/> ein.
/// </summary>
[Export(typeof(ITaggerProvider))]
[ContentType("csharp")]
[TagType(typeof(IntraTextGoToTag))]
sealed class IntraTextGoToTaggerProvider : ITaggerProvider {

    /// <summary>
    /// Liefert den (pro <paramref name="buffer"/> zwischengespeicherten) <see cref="IntraTextGoToTagger"/>,
    /// sofern der angeforderte Tag-Typ <typeparamref name="T"/> mit <see cref="IntraTextGoToTag"/> kompatibel ist.
    /// </summary>
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {

        if (buffer == null) {
            throw new ArgumentNullException(nameof(buffer));
        }

        return buffer.Properties.GetOrCreateSingletonProperty(() => new IntraTextGoToTagger(buffer)) as ITagger<T>;
    }
}