#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoTo; 

[ContentType(NavLanguageContentDefinitions.ContentType)]
[Name("Nav/" + nameof(GoToKeyProcessorProvider))]
[Export(typeof(IKeyProcessorProvider))]
[TextViewRole(PredefinedTextViewRoles.Interactive)]
sealed class GoToKeyProcessorProvider : IKeyProcessorProvider {

    readonly TextViewConnectionListener _textViewConnectionListener;

    [ImportingConstructor]
    public GoToKeyProcessorProvider(TextViewConnectionListener textViewConnectionListener) {
        _textViewConnectionListener = textViewConnectionListener;
    }

    public KeyProcessor GetAssociatedProcessor(IWpfTextView textView) {
        return GoToKeyProcessor.GetKeyProcessorForView(textView, _textViewConnectionListener);
    }
}