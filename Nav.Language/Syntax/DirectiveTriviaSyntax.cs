using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Der abstrakte Basistyp aller Präprozessor-Direktiven (<c>#…</c>) — nach dem Roslyn-Vorbild der
/// <c>DirectiveTriviaSyntax</c>. Eine Direktive ist syntaktisch nicht Teil der eigentlichen
/// Sprach-Grammatik (Tasks, Transitionen), sondern eine Anweisung an die Engine. Als strukturierte Trivia
/// (siehe <see cref="StructuredTriviaSyntax"/>) hält sie ihre <c>#</c>-Token in einer eigenen, lokalen
/// <see cref="SyntaxTokenList"/> und ist über die strukturierte
/// <see cref="SyntaxTokenType.DirectiveTrivia"/>-Trivia des Folge-Tokens erreichbar. Erste konkrete
/// Ausprägung ist <see cref="VersionDirectiveSyntax"/> (<c>#version</c>).
/// </summary>
[Serializable]
public abstract class DirectiveTriviaSyntax: StructuredTriviaSyntax {

    private protected DirectiveTriviaSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das einleitende <c>#</c> der Direktive.</summary>
    public SyntaxToken HashToken => ChildTokens().FirstOrMissing(SyntaxTokenType.HashToken);

}