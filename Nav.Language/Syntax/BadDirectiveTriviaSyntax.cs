using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Eine strukturell erfasste, aber wirkungslose Präprozessor-Direktive — nach dem Roslyn-Vorbild der
/// <c>BadDirectiveTriviaSyntax</c>. Dazu zählen unbekannte Direktiven (<c>#if</c> …, ebenso ein
/// <c>#pragma</c> ohne Subjekt; melden <c>Nav3000</c>) und unbekannte Pragmas (<c>#pragma warning</c> …,
/// die <c>Nav3001</c> melden). Eine deplatzierte (<c>Nav3003</c>) oder wiederholte (<c>Nav3004</c>)
/// <c>#version</c> ist dagegen <b>keine</b> Bad-Direktive, sondern bleibt eine — lediglich unwirksame —
/// <see cref="VersionDirectiveSyntax"/>. Der Knoten trägt seine Token lokal (siehe
/// <see cref="DirectiveTriviaSyntax"/>) und ist über die <see cref="SyntaxTokenType.DirectiveTrivia"/>-Trivia
/// erreichbar.
/// </summary>
[Serializable]
[SampleSyntax("#unknown")]
public sealed partial class BadDirectiveTriviaSyntax: DirectiveTriviaSyntax {

    internal BadDirectiveTriviaSyntax(TextExtent extent): base(extent) {
    }

}
