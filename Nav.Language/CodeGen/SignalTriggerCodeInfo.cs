#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

public sealed class SignalTriggerCodeInfo {

    SignalTriggerCodeInfo(TaskCodeInfo containingTask, string? triggerName, string? viewNodeName) {

        ContainingTask = containingTask ?? throw new ArgumentNullException(nameof(containingTask));
        TriggerName    = triggerName    ?? String.Empty;
        ViewNodeName   = viewNodeName   ?? String.Empty;
    }

    public TaskCodeInfo ContainingTask         { get; }
    public string       ViewNodeName           { get; }
    public string       TriggerName            { get; }
    public string       TriggerLogicMethodName => $"{TriggerName}{ContainingTask.Facts.LogicMethodSuffix}";
    public string       TOClassName            => $"{ViewNodeName.ToPascalcase()}{CodeGenInvariants.ToClassNameSuffix}";

    public static SignalTriggerCodeInfo FromSignalTrigger(ISignalTriggerSymbol signalTriggerSymbol) {
        return FromSignalTrigger(signalTriggerSymbol, null);
    }

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