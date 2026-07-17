using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Die Code-Deklaration <c>[using …]</c> im Datei-Kopf (<see cref="CodeGenerationUnitSyntax.CodeUsings"/>),
/// z.B. <c>[using System.Linq]</c> — eine zusätzliche <c>using</c>-Direktive im generierten C#-Code.
/// Als einzige Code-Deklaration beliebig oft wiederholbar (<see cref="CodeBlockFacts.IsRepeatable"/>);
/// doppelte Namespaces meldet der semantische Analyzer (Nav1002). Zulässig nur im Datei-Kopf
/// (<see cref="CodeBlockFacts"/>).
/// </summary>
[Serializable]
[SampleSyntax("[using Namespace]")]
public sealed partial class CodeUsingDeclarationSyntax: CodeSyntax {

    internal CodeUsingDeclarationSyntax(TextExtent extent, IdentifierOrStringSyntax? namespaceSyntax): base(extent) {
        AddChildNode(Namespace = namespaceSyntax);
    }

    /// <summary>Das Schlüsselwort <c>using</c>.</summary>
    public SyntaxToken UsingKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.UsingKeyword);

    /// <summary>
    /// Der Namespace — als Identifier oder String-Literal notierbar; <c>null</c>, wenn im Quelltext
    /// an dieser Stelle keines von beidem steht.
    /// </summary>
    public IdentifierOrStringSyntax? Namespace { get; }

    /// <summary>Eine <c>[using …]</c>-Deklaration enthält nie ihresgleichen — beschleunigt <see cref="SyntaxNode.DescendantNodes{T}()"/>.</summary>
    private protected override bool PromiseNoDescendantNodeOfSameType => true;

}