using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Der abstrakte Basistyp aller Präprozessor-Direktiven (<c>#…</c>) — nach dem Roslyn-Vorbild der
/// <c>DirectiveTriviaSyntax</c>. Eine Direktive ist syntaktisch nicht Teil der eigentlichen
/// Sprach-Grammatik (Tasks, Transitionen), sondern eine Anweisung an die Engine. Nach dem Zielmodell hält
/// sie ihre <c>#</c>-Token in einer <b>eigenen, lokalen</b> <see cref="SyntaxTokenList"/> (nicht im flachen
/// <see cref="SyntaxTree.Tokens"/>-Strom) und ist über die strukturierte
/// <see cref="SyntaxTokenType.DirectiveTrivia"/>-Trivia des Folge-Tokens erreichbar. Erste konkrete
/// Ausprägung ist <see cref="VersionDirectiveSyntax"/> (<c>#pragma version</c>).
/// </summary>
[Serializable]
public abstract class DirectiveTriviaSyntax: SyntaxNode {

    readonly SyntaxTokenList _localTokens;

    private protected DirectiveTriviaSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>
    /// Erzeugt eine Direktive, deren Token in einer eigenen, lokalen <paramref name="localTokens"/>-Liste
    /// liegen (Zielmodell strukturierter Trivia) statt im globalen Token-Strom des Baums.
    /// </summary>
    private protected DirectiveTriviaSyntax(TextExtent extent, SyntaxTokenList localTokens): base(extent) {
        _localTokens = localTokens;
    }

    /// <summary>
    /// Die Token dieser Direktive. Liegen sie lokal vor (Zielmodell strukturierter Trivia), wird die eigene
    /// Liste geliefert; andernfalls (solange die Token noch im flachen Strom stehen) das Verhalten der Basis.
    /// </summary>
    public override IEnumerable<SyntaxToken> ChildTokens() {
        return _localTokens ?? base.ChildTokens();
    }

    /// <summary>Das einleitende <c>#</c> der Direktive.</summary>
    public SyntaxToken HashToken => ChildTokens().FirstOrMissing(SyntaxTokenType.HashToken);

}