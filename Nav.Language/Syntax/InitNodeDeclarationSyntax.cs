using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Deklaration eines <c>init</c>-Knotens, z.B. <c>init I1 [params bool refresh];</c> — ein
/// Einstiegspunkt des Tasks und zugleich Verbindungspunkt (von außen ansprechbar, siehe
/// <see cref="ConnectionPointNodeSyntax"/>). Der Name ist optional; als Code-Annotationen sind
/// <c>[abstractmethod]</c> und <c>[params …]</c> zulässig (Autorität: <see cref="CodeBlockFacts"/>),
/// dahinter optional eine <c>do</c>-Klausel (<see cref="DoClauseSyntax"/>).
/// </summary>
[Serializable]
[SampleSyntax("init Identifier [abstractmethod] [params T1 param1, T2<T3, T4<T5>> param2, T6[][] param3] do Instruction;")]
public partial class InitNodeDeclarationSyntax: ConnectionPointNodeSyntax {

    internal InitNodeDeclarationSyntax(TextExtent extent,
                                       CodeAbstractMethodDeclarationSyntax? codeAbstractMethodDeclaration,
                                       CodeParamsDeclarationSyntax? codeParamsDeclaration,
                                       DoClauseSyntax? doClause)
        : base(extent) {

        AddChildNode(CodeAbstractMethodDeclaration = codeAbstractMethodDeclaration);
        AddChildNode(CodeParamsDeclaration         = codeParamsDeclaration);
        AddChildNode(DoClause                      = doClause);
    }

    /// <summary>Das Schlüsselwort <c>init</c>.</summary>
    public SyntaxToken InitKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.InitKeyword);

    /// <summary>
    /// Der Name des Init-Knotens — optional: ein Missing-Token (<see cref="SyntaxToken.IsMissing"/>)
    /// bei einem namenlosen <c>init;</c>.
    /// </summary>
    public SyntaxToken Identifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

    /// <summary>Die optionale <c>[params …]</c>-Deklaration — <c>null</c>, wenn nicht angegeben.</summary>
    public CodeParamsDeclarationSyntax? CodeParamsDeclaration { get; }

    /// <summary>Die optionale <c>[abstractmethod]</c>-Deklaration — <c>null</c>, wenn nicht angegeben.</summary>
    public CodeAbstractMethodDeclarationSyntax? CodeAbstractMethodDeclaration { get; }

    /// <summary>Die optionale <c>do</c>-Klausel — <c>null</c>, wenn nicht angegeben.</summary>
    public DoClauseSyntax? DoClause { get; }

}