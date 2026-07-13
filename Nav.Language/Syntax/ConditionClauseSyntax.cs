using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Die Bedingungs-Klausel (Guard) einer Transition bzw. Exit-Transition — der Übergang gilt nur, wenn
/// die Bedingung zutrifft. Drei Formen: <c>if Bedingung</c> (<see cref="IfConditionClauseSyntax"/>),
/// <c>else</c> (<see cref="ElseConditionClauseSyntax"/>) und <c>else if Bedingung</c>
/// (<see cref="ElseIfConditionClauseSyntax"/>).
/// </summary>
[Serializable]
public abstract class ConditionClauseSyntax: SyntaxNode {

    /// <summary>Initialisiert die Basisklasse mit dem Quelltext-Bereich der Klausel.</summary>
    protected ConditionClauseSyntax(TextExtent extent): base(extent) {
    }

}

/// <summary>
/// Eine <c>if</c>-Bedingung, z.B. <c>Choice1 --&gt; Ziel if IstGültig;</c> — die Bedingung selbst wird
/// als Bezeichner oder String-Literal angegeben.
/// </summary>
[Serializable]
[SampleSyntax("if Condition")]
public partial class IfConditionClauseSyntax: ConditionClauseSyntax {

    internal IfConditionClauseSyntax(TextExtent extent, IdentifierOrStringSyntax? identifierOrString): base(extent) {
        AddChildNode(IdentifierOrString = identifierOrString);
    }

    /// <summary>Das Schlüsselwort <c>if</c>.</summary>
    public SyntaxToken IfKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.IfKeyword);

    /// <summary>Die Bedingung als Bezeichner oder String-Literal — <c>null</c>, wenn sie im Quelltext fehlt.</summary>
    public IdentifierOrStringSyntax? IdentifierOrString { get; }

}

/// <summary>
/// Ein <c>else</c> ohne eigene Bedingung — der Alternativzweig zu einer <c>if</c>-Bedingung, z.B.
/// <c>Choice1 --&gt; Ziel else;</c>. Als Bestandteil von <c>else if</c> tritt sie zusätzlich innerhalb
/// einer <see cref="ElseIfConditionClauseSyntax"/> auf.
/// </summary>
[Serializable]
[SampleSyntax("else")]
public partial class ElseConditionClauseSyntax: ConditionClauseSyntax {

    internal ElseConditionClauseSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>Das Schlüsselwort <c>else</c>.</summary>
    public SyntaxToken ElseKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.ElseKeyword);

}

/// <summary>
/// Eine <c>else if</c>-Bedingung, z.B. <c>Choice1 --&gt; Ziel else if IstGültig;</c> — zusammengesetzt
/// aus dem <c>else</c>-Teil (<see cref="ElseCondition"/>) und der nachfolgenden <c>if</c>-Bedingung
/// (<see cref="IfCondition"/>).
/// </summary>
[Serializable]
[SampleSyntax("else if Condition")]
public partial class ElseIfConditionClauseSyntax: ConditionClauseSyntax {

    internal ElseIfConditionClauseSyntax(TextExtent extent, ElseConditionClauseSyntax elseCondition, IfConditionClauseSyntax ifCondition): base(extent) {
        AddChildNode(ElseCondition = elseCondition);
        AddChildNode(IfCondition   = ifCondition);
    }

    /// <summary>Der <c>else</c>-Teil der Klausel.</summary>
    public ElseConditionClauseSyntax ElseCondition { get; }

    /// <summary>Die <c>if</c>-Bedingung hinter dem <c>else</c>.</summary>
    public IfConditionClauseSyntax IfCondition { get; }

}