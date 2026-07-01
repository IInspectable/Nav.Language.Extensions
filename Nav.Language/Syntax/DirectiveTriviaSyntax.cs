using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Der abstrakte Basistyp aller Präprozessor-Direktiven (<c>#…</c>) — nach dem Roslyn-Vorbild der
/// <c>DirectiveTriviaSyntax</c>. Eine Direktive ist syntaktisch nicht Teil der eigentlichen
/// Sprach-Grammatik (Tasks, Transitionen), sondern eine Anweisung an die Engine; sie hängt daher als
/// eigener Knoten am Kopf der <see cref="CodeGenerationUnitSyntax"/> und trägt ihre einleitenden
/// <c>#</c>-Token als direkt angehängte Token. Erste konkrete Ausprägung ist
/// <see cref="VersionDirectiveSyntax"/> (<c>#pragma version</c>).
/// </summary>
[Serializable]
public abstract class DirectiveTriviaSyntax: SyntaxNode {

    private protected DirectiveTriviaSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das einleitende <c>#</c> der Direktive.</summary>
    public SyntaxToken HashToken => ChildTokens().FirstOrMissing(SyntaxTokenType.HashToken);

}