using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Die Code-Deklaration <c>[namespaceprefix …]</c>, z.B. <c>[namespaceprefix Pharmatechnik.Apotheke]</c> —
/// das Namespace-Präfix für den generierten C#-Code. Zwei Hosts (<see cref="CodeBlockFacts"/>): im
/// Datei-Kopf (<see cref="CodeGenerationUnitSyntax.CodeNamespace"/>) gilt sie für die aus der Datei
/// generierten Klassen; an einer <c>taskref</c>-Deklaration
/// (<see cref="TaskDeclarationSyntax.CodeNamespaceDeclaration"/>) benennt sie den Namespace des
/// referenzierten Tasks (<see cref="ITaskDeclarationSymbol.CodeNamespace"/>).
/// </summary>
[Serializable]
[SampleSyntax("[namespaceprefix Namespace]")]
public sealed partial class CodeNamespaceDeclarationSyntax: CodeSyntax {

    internal CodeNamespaceDeclarationSyntax(TextExtent extent, IdentifierOrStringSyntax? namespaceSyntax)
        : base(extent) {

        AddChildNode(Namespace = namespaceSyntax);
    }

    /// <summary>Das Schlüsselwort <c>namespaceprefix</c>.</summary>
    public SyntaxToken NamespaceprefixKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.NamespaceprefixKeyword);

    /// <summary>
    /// Der Namespace — als Identifier oder String-Literal notierbar; <c>null</c>, wenn im Quelltext
    /// an dieser Stelle keines von beidem steht.
    /// </summary>
    public IdentifierOrStringSyntax? Namespace { get; }

    /// <summary>Eine <c>[namespaceprefix …]</c>-Deklaration enthält nie ihresgleichen — beschleunigt <see cref="SyntaxNode.DescendantNodes{T}()"/>.</summary>
    private protected override bool PromiseNoDescendantNodeOfSameType => true;

}