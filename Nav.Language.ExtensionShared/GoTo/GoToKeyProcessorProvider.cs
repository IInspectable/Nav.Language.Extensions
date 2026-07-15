#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoTo; 

/// <summary>
/// Der MEF-Provider, der je interaktiver Nav-Editor-Sicht den <see cref="GoToKeyProcessor"/> beisteuert
/// (Inhaltstyp <see cref="NavLanguageContentDefinitions.ContentType"/>). Bezieht den geteilten
/// <see cref="TextViewConnectionListener"/> per Import, über den sich der Prozessor beim Trennen der
/// Sicht wieder abmeldet.
/// </summary>
[ContentType(NavLanguageContentDefinitions.ContentType)]
[Name("Nav/" + nameof(GoToKeyProcessorProvider))]
[Export(typeof(IKeyProcessorProvider))]
[TextViewRole(PredefinedTextViewRoles.Interactive)]
sealed class GoToKeyProcessorProvider : IKeyProcessorProvider {

    readonly TextViewConnectionListener _textViewConnectionListener;

    /// <summary>Bezieht den geteilten <see cref="TextViewConnectionListener"/> per MEF-Import.</summary>
    [ImportingConstructor]
    public GoToKeyProcessorProvider(TextViewConnectionListener textViewConnectionListener) {
        _textViewConnectionListener = textViewConnectionListener;
    }

    /// <summary>Liefert den <see cref="GoToKeyProcessor"/> (Singleton) für <paramref name="textView"/>.</summary>
    public KeyProcessor GetAssociatedProcessor(IWpfTextView textView) {
        return GoToKeyProcessor.GetKeyProcessorForView(textView, _textViewConnectionListener);
    }
}
