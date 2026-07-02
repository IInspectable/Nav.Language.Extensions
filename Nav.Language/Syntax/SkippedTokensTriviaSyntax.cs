#nullable enable

using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Vom Parser übersprungener Quelltext als strukturierte Trivia — nach dem Roslyn-Vorbild der
/// <c>SkippedTokensTriviaSyntax</c>. Ein maximaler Lauf benachbarter Token, die der Panic-Mode übersprungen
/// hat (Deletion-Recovery, z.B. das <c>[]</c> in <c>init [];</c>) oder die lexikalisch unbekannt sind
/// (<see cref="SyntaxTokenType.Unknown"/>), wird zu genau einem solchen Knoten gefaltet; reine Trivia
/// zwischen den Token bricht den Lauf nicht (sie fällt in dessen Extent). Der Knoten trägt seine Token lokal
/// (Klassifikation <see cref="TextClassification.Skiped"/>, siehe <see cref="SyntaxNode.ChildTokens"/>) und
/// ist über die angehängte <see cref="SyntaxTokenType.SkippedTokensTrivia"/>-Trivia erreichbar (siehe
/// <see cref="SyntaxTree.SkippedTokens"/>); im flachen <see cref="SyntaxTree.Tokens"/>-Strom stehen die
/// übersprungenen Token nicht mehr.
/// </summary>
[Serializable]
public sealed partial class SkippedTokensTriviaSyntax: StructuredTriviaSyntax {

    internal SkippedTokensTriviaSyntax(TextExtent extent): base(extent) {
    }

}
