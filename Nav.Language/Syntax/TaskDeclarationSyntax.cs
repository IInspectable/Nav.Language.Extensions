#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Die Deklaration eines fremden Tasks, z.B. <c>taskref Name { init I1; exit E1; }</c> — macht die
/// Schnittstelle eines anderweitig definierten Tasks bekannt: seine Verbindungspunkte
/// (<c>init</c>/<c>exit</c>/<c>end</c>, siehe <see cref="ConnectionPoints"/>) sowie optionale
/// Code-Deklarationen (<c>[namespaceprefix …]</c>, <c>[notimplemented]</c>, <c>[result …]</c>).
/// Damit kann eine Task-Definition den fremden Task als Task-Knoten aufrufen, ohne dessen Definition
/// zu kennen. Semantisches Gegenstück ist das Task-Declaration-Symbol
/// (<c>TaskDeclarationSymbolBuilder</c>); nicht zu verwechseln mit der Include-Direktive
/// <c>taskref "datei.nav";</c> (<see cref="IncludeDirectiveSyntax"/>), die eine ganze Datei einbindet.
/// </summary>
[Serializable]
[SampleSyntax("taskref Task { };")]
public partial class TaskDeclarationSyntax: MemberDeclarationSyntax {

    internal TaskDeclarationSyntax(TextExtent extent,
                                   CodeNamespaceDeclarationSyntax? codeNamespaceDeclaration,
                                   CodeNotImplementedDeclarationSyntax? codeNotImplementedDeclaration,
                                   CodeResultDeclarationSyntax? codeResultDeclaration,
                                   IReadOnlyList<ConnectionPointNodeSyntax> connectionPoints)
        : base(extent) {

        AddChildNode(CodeNamespaceDeclaration      = codeNamespaceDeclaration);
        AddChildNode(CodeNotImplementedDeclaration = codeNotImplementedDeclaration);
        AddChildNode(CodeResultDeclaration         = codeResultDeclaration);
        AddChildNodes(ConnectionPoints             = connectionPoints);
    }

    /// <summary>Das Schlüsselwort <c>taskref</c>.</summary>
    public SyntaxToken TaskrefKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.TaskrefKeyword);
    /// <summary>Der Name des deklarierten Tasks.</summary>
    public SyntaxToken Identifier     => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);
    /// <summary>Die öffnende geschweifte Klammer <c>{</c> des Deklarations-Rumpfs.</summary>
    public SyntaxToken OpenBrace      => ChildTokens().FirstOrMissing(SyntaxTokenType.OpenBrace);
    /// <summary>Die schließende geschweifte Klammer <c>}</c> des Deklarations-Rumpfs.</summary>
    public SyntaxToken CloseBrace     => ChildTokens().FirstOrMissing(SyntaxTokenType.CloseBrace);

    /// <summary>Die optionale <c>[namespaceprefix …]</c>-Deklaration — <c>null</c>, wenn nicht angegeben.</summary>
    public CodeNamespaceDeclarationSyntax? CodeNamespaceDeclaration { get; }

    /// <summary>Die optionale <c>[notimplemented]</c>-Deklaration — <c>null</c>, wenn nicht angegeben.</summary>
    public CodeNotImplementedDeclarationSyntax? CodeNotImplementedDeclaration { get; }

    /// <summary>Die optionale <c>[result …]</c>-Deklaration — <c>null</c>, wenn nicht angegeben.</summary>
    public CodeResultDeclarationSyntax? CodeResultDeclaration { get; }

    /// <summary>
    /// Die Verbindungspunkte des Rumpfs (<c>init</c>-, <c>exit</c>- und <c>end</c>-Deklarationen) in
    /// Quelltext-Reihenfolge.
    /// </summary>
    public IReadOnlyList<ConnectionPointNodeSyntax> ConnectionPoints { get; }

    /// <summary>Die <c>init</c>-Deklarationen unter den <see cref="ConnectionPoints"/>.</summary>
    public IEnumerable<InitNodeDeclarationSyntax> InitNodes() {
        return ConnectionPoints.OfType<InitNodeDeclarationSyntax>();
    }

    /// <summary>Die <c>exit</c>-Deklarationen unter den <see cref="ConnectionPoints"/>.</summary>
    public IEnumerable<ExitNodeDeclarationSyntax> ExitNodes() {
        return ConnectionPoints.OfType<ExitNodeDeclarationSyntax>();
    }

    /// <summary>Die <c>end</c>-Deklarationen unter den <see cref="ConnectionPoints"/>.</summary>
    public IEnumerable<EndNodeDeclarationSyntax> EndNodes() {
        return ConnectionPoints.OfType<EndNodeDeclarationSyntax>();
    }

    /// <summary>Eine Task-Deklaration enthält nie ihresgleichen — beschleunigt <see cref="SyntaxNode.DescendantNodes{T}()"/>.</summary>
    private protected override bool PromiseNoDescendantNodeOfSameType => true;

}