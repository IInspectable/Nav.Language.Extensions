#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Die Nav→C#-Anker eines Init-Knotens: der enthaltende Task, der Init-Name und der Name der
/// generierten Begin-Logic-Methode <c>BeginLogic</c>. Trägt die Navigation vom Init-Knoten in den
/// generierten Code. <b>Versionsrichtig:</b> Begin-Präfix und Logic-Suffix werden über
/// <see cref="TaskCodeInfo.Facts"/> aus der Sprach-Version des Nav-Symbols bezogen.
/// </summary>
public sealed class TaskInitCodeInfo {

    TaskInitCodeInfo(TaskCodeInfo containingTask, string? initName) {

        ContainingTask       = containingTask ?? throw new ArgumentNullException(nameof(containingTask));
        BeginLogicMethodName = $"{containingTask.Facts.BeginMethodPrefix}{containingTask.Facts.LogicMethodSuffix}";
        InitName             = initName ?? String.Empty;
    }

    /// <summary>Der den Init umschließende Task als Namens-/Pfad-Anker (<see cref="TaskCodeInfo"/>).</summary>
    public TaskCodeInfo ContainingTask       { get; }
    /// <summary>
    /// Der Name der generierten Begin-Logic-Methode (<c>BeginLogic</c>). Ziel der Navigation vom
    /// Init-Knoten in den C#-Code; Präfix und Suffix stammen versionsrichtig aus
    /// <see cref="ICodeGenFacts.BeginMethodPrefix"/> und <see cref="ICodeGenFacts.LogicMethodSuffix"/>
    /// (via <see cref="ContainingTask"/>).
    /// </summary>
    public string       BeginLogicMethodName { get; }
    /// <summary>Der Name des Init-Knotens (leer, wenn abwesend).</summary>
    public string       InitName             { get; }

    /// <summary>
    /// Factory: baut die <see cref="TaskInitCodeInfo"/> zu einem Init-Knoten-Symbol; leitet den
    /// enthaltenden Task über <see cref="TaskCodeInfo.FromTaskDefinition"/> ab.
    /// </summary>
    public static TaskInitCodeInfo FromInitNode(IInitNodeSymbol initNodeSymbol) {

        if (initNodeSymbol == null) {
            throw new ArgumentNullException(nameof(initNodeSymbol));
        }

        var taskCodeModel = TaskCodeInfo.FromTaskDefinition(initNodeSymbol.ContainingTask);

        return FromInitNode(taskCodeModel, initNodeSymbol);
    }

    /// <summary>
    /// Factory-Überladung, die den bereits gebauten <see cref="TaskCodeInfo"/> des enthaltenden Tasks
    /// wiederverwendet (<paramref name="taskCodeInfo"/>) statt ihn erneut abzuleiten.
    /// </summary>
    internal static TaskInitCodeInfo FromInitNode(TaskCodeInfo taskCodeInfo, IInitNodeSymbol initNodeSymbol) {

        if (initNodeSymbol == null) {
            throw new ArgumentNullException(nameof(initNodeSymbol));
        }

        if (taskCodeInfo == null) {
            throw new ArgumentNullException(nameof(taskCodeInfo));
        }

        return new TaskInitCodeInfo(containingTask: taskCodeInfo, initName: initNodeSymbol.Name);
    }

}