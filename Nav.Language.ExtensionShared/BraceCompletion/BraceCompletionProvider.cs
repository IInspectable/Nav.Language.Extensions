#region Using Directives

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.BraceCompletion;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.BraceCompletion; 

/// <summary>
/// Aktiviert die automatische Klammer-Vervollständigung des VS-Editors für Nav-Dokumente: Beim Tippen
/// einer öffnenden Klammer bzw. eines Anführungszeichens fügt VS das passende Gegenstück ein. Die
/// unterstützten Paare (<c>{}</c>, <c>()</c>, <c>[]</c>, <c>""</c>) werden per <c>[BracePair]</c>
/// deklariert; die eigentliche Vervollständigungslogik übernimmt der Editor über
/// <see cref="IBraceCompletionDefaultProvider"/>.
/// </summary>
[Export(typeof(IBraceCompletionDefaultProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[BracePair('{', '}')]
[BracePair('(', ')')]
[BracePair('[', ']')]
[BracePair('"', '"')]
class BraceCompletionProvider : IBraceCompletionDefaultProvider {
}