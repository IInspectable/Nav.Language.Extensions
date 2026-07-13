using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Die Include-Direktive <c>taskref "datei.nav";</c> — bindet eine andere <c>.nav</c>-Datei ein und
/// macht deren Tasks referenzierbar. Semantisches Gegenstück ist <see cref="IIncludeSymbol"/>.
/// Nutzt dasselbe Schlüsselwort wie die Task-Deklaration <see cref="TaskDeclarationSyntax"/>
/// (<c>taskref Name { … }</c>); der Parser unterscheidet die beiden am Folgetoken
/// (String-Literal ⇒ Include-Direktive, Bezeichner ⇒ Task-Deklaration).
/// </summary>
[Serializable]
[SampleSyntax("taskref \"file.nav\";")]
public sealed partial class IncludeDirectiveSyntax: MemberDeclarationSyntax {

    internal IncludeDirectiveSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das Schlüsselwort <c>taskref</c>.</summary>
    public SyntaxToken TaskrefKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.TaskrefKeyword);
    /// <summary>Das String-Literal mit dem Pfad der eingebundenen Datei (inklusive Anführungszeichen).</summary>
    public SyntaxToken StringLiteral  => ChildTokens().FirstOrMissing(SyntaxTokenType.StringLiteral);
    /// <summary>Das abschließende Semikolon <c>;</c>.</summary>
    public SyntaxToken Semicolon      => ChildTokens().FirstOrMissing(SyntaxTokenType.Semicolon);

    /// <summary>Eine Include-Direktive enthält nie ihresgleichen — beschleunigt <see cref="SyntaxNode.DescendantNodes{T}()"/>.</summary>
    private protected override bool PromiseNoDescendantNodeOfSameType => true;

}