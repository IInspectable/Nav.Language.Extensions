using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Der Auslöser einer Transition (<see cref="TransitionDefinitionSyntax.Trigger"/>). Zwei Arten:
/// ein Signal-Trigger <c>on Signal</c> (<see cref="SignalTriggerSyntax"/>) oder ein spontaner Übergang
/// <c>spontaneous</c>/<c>spont</c> ohne explizites Signal (<see cref="SpontaneousTriggerSyntax"/>).
/// </summary>
[Serializable]
public abstract class TriggerSyntax: SyntaxNode {

    /// <summary>Initialisiert die Basisklasse mit dem Quelltext-Bereich des Triggers.</summary>
    protected TriggerSyntax(TextExtent extent): base(extent) {
    }

}

/// <summary>
/// Ein spontaner Übergang ohne explizites Signal, z.B. <c>View --&gt; Ziel spontaneous;</c> —
/// geschrieben als <c>spontaneous</c> oder in der Kurzform <c>spont</c>.
/// </summary>
[Serializable]
[SampleSyntax("spontaneous")]
public partial class SpontaneousTriggerSyntax: TriggerSyntax {

    internal SpontaneousTriggerSyntax(TextExtent extent): base(extent) {
    }

    /// <summary>
    /// Das Schlüsselwort <c>spontaneous</c>. Bei der Kurzform <c>spont</c> trägt der Knoten stattdessen
    /// ein Token vom Typ <see cref="SyntaxTokenType.SpontKeyword"/> — diese Property liefert dann ein
    /// Missing-Token (<see cref="SyntaxToken.IsMissing"/>).
    /// </summary>
    public SyntaxToken SpontaneousKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.SpontaneousKeyword);

    /// <summary>
    /// Das kanonische Schlüsselwort-Literal <c>"spontaneous"</c> — zugleich der Symbol-Name des
    /// zugehörigen Trigger-Symbols (<see cref="ISpontaneousTriggerSymbol"/>), auch bei der
    /// Kurzform <c>spont</c>.
    /// </summary>
    public const string Keyword = "spontaneous";

}

/// <summary>
/// Ein Signal-Trigger, z.B. <c>View --&gt; Ziel on Speichern;</c> — benennt das Signal, das den
/// Übergang auslöst (→ <see cref="ISignalTriggerSymbol"/>).
/// </summary>
[Serializable]
[SampleSyntax("on Trigger")]
public partial class SignalTriggerSyntax: TriggerSyntax {

    internal SignalTriggerSyntax(TextExtent extent, IdentifierSyntax? identifier)
        : base(extent) {
        AddChildNode(Identifier = identifier);
    }

    /// <summary>Das Schlüsselwort <c>on</c>.</summary>
    public SyntaxToken OnKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.OnKeyword);

    /// <summary>Der Name des Signals — <c>null</c>, wenn er im Quelltext fehlt.</summary>
    public IdentifierSyntax? Identifier { get; }

}