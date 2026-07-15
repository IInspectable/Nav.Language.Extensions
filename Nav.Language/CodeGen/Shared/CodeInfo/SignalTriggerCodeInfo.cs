#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Die Nav→C#-Anker eines Signal-Triggers: der enthaltende Task, der Trigger- und der (auslösende)
/// View-Knoten-Name, daraus abgeleitet der Name der generierten <c>{Trigger}Logic</c>-Methode und der
/// TO-Typname des Views. Trägt QuickInfo-Anzeige (<c>DisplayPartsBuilder</c>) und Navigation in den
/// generierten Code. <b>Versionsrichtig:</b> der Logic-Suffix wird — wie bei
/// <see cref="TaskExitCodeInfo"/>/<see cref="ChoiceCodeInfo"/> — über <see cref="TaskCodeInfo.Facts"/>
/// aus der Sprach-Version des Nav-Symbols bezogen; der TO-Suffix ist invariant
/// (<see cref="CodeGenInvariants.ToClassNameSuffix"/>).
/// </summary>
public sealed class SignalTriggerCodeInfo {

    SignalTriggerCodeInfo(TaskCodeInfo containingTask, string? triggerName, string? viewNodeName) {

        ContainingTask = containingTask ?? throw new ArgumentNullException(nameof(containingTask));
        TriggerName    = triggerName    ?? String.Empty;
        ViewNodeName   = viewNodeName   ?? String.Empty;
    }

    /// <summary>Der den Trigger umschließende Task als Namens-/Pfad-Anker (<see cref="TaskCodeInfo"/>).</summary>
    public TaskCodeInfo ContainingTask         { get; }
    /// <summary>Der Name des auslösenden View-Knotens; Basis des <see cref="TOClassName"/> (leer, wenn abwesend).</summary>
    public string       ViewNodeName           { get; }
    /// <summary>Der Signal-/Trigger-Name; Basis des <see cref="TriggerLogicMethodName"/> (leer, wenn abwesend).</summary>
    public string       TriggerName            { get; }
    /// <summary>
    /// Der Name der generierten Trigger-Logic-Methode (<c>{Trigger}Logic</c>). Ziel der Navigation vom
    /// Trigger in den C#-Code und der QuickInfo-Signatur; der Suffix <c>Logic</c> stammt versionsrichtig
    /// aus <see cref="ICodeGenFacts.LogicMethodSuffix"/> (via <see cref="ContainingTask"/>).
    /// </summary>
    public string       TriggerLogicMethodName => $"{TriggerName}{ContainingTask.Facts.LogicMethodSuffix}";
    /// <summary>
    /// Der Typname des Transfer-Objekts des auslösenden Views (<c>{View}TO</c>) — der Parametertyp der
    /// Trigger-Methode. Invariant, da in der Interface-Signatur sichtbar
    /// (<see cref="CodeGenInvariants.ToClassNameSuffix"/>).
    /// </summary>
    public string       TOClassName            => $"{ViewNodeName.ToPascalcase()}{CodeGenInvariants.ToClassNameSuffix}";

    /// <summary>Factory: baut die <see cref="SignalTriggerCodeInfo"/> zu einem Signal-Trigger-Symbol.</summary>
    public static SignalTriggerCodeInfo FromSignalTrigger(ISignalTriggerSymbol signalTriggerSymbol) {
        return FromSignalTrigger(signalTriggerSymbol, null);
    }

    /// <summary>
    /// Factory-Überladung, die den bereits gebauten <see cref="TaskCodeInfo"/> des enthaltenden Tasks
    /// wiederverwendet (<paramref name="taskCodeInfo"/>); ist er <c>null</c>, wird er über
    /// <see cref="TaskCodeInfo.FromTaskDefinition"/> aus dem Transitions-Task abgeleitet. Der View-Name
    /// stammt aus der GUI-Knoten-Quelle der Transition (leer, wenn keine vorhanden).
    /// </summary>
    internal static SignalTriggerCodeInfo FromSignalTrigger(ISignalTriggerSymbol signalTriggerSymbol, TaskCodeInfo? taskCodeInfo) {

        if (signalTriggerSymbol == null) {
            throw new ArgumentNullException(nameof(signalTriggerSymbol));
        }

        var task         = signalTriggerSymbol.Transition.ContainingTask;
        var viewNodeName = signalTriggerSymbol.Transition.GuiNodeSourceReference?.Declaration?.Name ?? String.Empty;
        var triggerName  = signalTriggerSymbol.Name;

        return new SignalTriggerCodeInfo(
            containingTask: taskCodeInfo ?? TaskCodeInfo.FromTaskDefinition(task),
            triggerName: triggerName,
            viewNodeName: viewNodeName
        );
    }

}