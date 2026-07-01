using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Jede Präprozessor-Direktive, die keine wirksame <see cref="VersionDirectiveSyntax"/> ist — nach dem
/// Roslyn-Vorbild der <c>BadDirectiveTriviaSyntax</c>. Dazu zählen unbekannte Direktiven (<c>#if</c>,
/// <c>#pragma warning</c> …, die <c>Nav3000</c>/<c>Nav3001</c> melden) ebenso wie eine deplatzierte
/// (<c>Nav3003</c>) oder wiederholte (<c>Nav3004</c>) <c>#pragma version</c>: strukturell erfasst, aber ohne
/// Wirkung auf die Sprach-Version. Der Knoten trägt seine Token lokal (siehe
/// <see cref="DirectiveTriviaSyntax"/>) und ist über die <see cref="SyntaxTokenType.DirectiveTrivia"/>-Trivia
/// erreichbar.
/// </summary>
[Serializable]
[SampleSyntax("#unknown")]
public sealed partial class BadDirectiveTriviaSyntax: DirectiveTriviaSyntax {

    internal BadDirectiveTriviaSyntax(TextExtent extent): base(extent) {
    }

}
