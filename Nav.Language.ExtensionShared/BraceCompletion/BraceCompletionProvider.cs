#region Using Directives

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.BraceCompletion;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.BraceCompletion; 

[Export(typeof(IBraceCompletionDefaultProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[BracePair('{', '}')]
[BracePair('(', ')')]
[BracePair('[', ']')]
[BracePair('"', '"')]
class BraceCompletionProvider : IBraceCompletionDefaultProvider {
}