#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Codemodell der <c>{Task}WFS</c>-OneShot-Datei der <b>Generation 2</b> — die (einmalig erzeugte)
/// Benutzer-Datei mit je einem <c>NotImplementedException</c>-Stub für die abstrakten Logic-Methoden
/// aus <c>{Task}WFSBase</c>. Anders als V1 überschreiben die Stubs die neuen
/// <c>{Context}.Result …Logic(args, {Context} next)</c>-Signaturen (bzw. bei
/// <c>[abstract]</c>-Quellen die Maschinerie-Methode selbst).
/// </summary>
sealed class WfsCodeModelV2: FileGenerationCodeModel {

    WfsCodeModelV2(TaskCodeInfo taskCodeInfo,
                   string relativeSyntaxFileName,
                   string filePath,
                   ImmutableList<string> usingNamespaces,
                   ImmutableList<TransitionCallContextCodeModel> initTransitions,
                   ImmutableList<TransitionCallContextCodeModel> exitTransitions,
                   ImmutableList<TransitionCallContextCodeModel> triggerTransitions,
                   ImmutableList<ChoiceCallContextCodeModel> choices)
        : base(taskCodeInfo, relativeSyntaxFileName, filePath) {

        UsingNamespaces    = usingNamespaces    ?? throw new ArgumentNullException(nameof(usingNamespaces));
        InitTransitions    = initTransitions    ?? throw new ArgumentNullException(nameof(initTransitions));
        ExitTransitions    = exitTransitions    ?? throw new ArgumentNullException(nameof(exitTransitions));
        TriggerTransitions = triggerTransitions ?? throw new ArgumentNullException(nameof(triggerTransitions));
        Choices            = choices            ?? throw new ArgumentNullException(nameof(choices));
    }

    /// <summary>Der Ziel-Namespace der OneShot-Datei (durchgereicht aus <see cref="TaskCodeInfo"/>).</summary>
    public string WflNamespace => Task.WflNamespace;
    /// <summary>Typname der partiellen Implementierungsklasse <c>{Task}WFS</c> (durchgereicht aus <see cref="TaskCodeInfo"/>).</summary>
    public string WfsTypeName   => Task.WfsTypeName;

    /// <summary>Die (sortierten) <c>using</c>-Namespaces der OneShot-Datei.</summary>
    public ImmutableList<string>                         UsingNamespaces    { get; }
    /// <summary>Die Init-Transitionen, deren <c>Begin{…}Logic</c>-Stub erzeugt wird.</summary>
    public ImmutableList<TransitionCallContextCodeModel> InitTransitions    { get; }
    /// <summary>Die Exit-Transitionen, deren <c>After{Node}Logic</c>-Stub erzeugt wird.</summary>
    public ImmutableList<TransitionCallContextCodeModel> ExitTransitions    { get; }
    /// <summary>Die Trigger-Transitionen, deren <c>{Trigger}Logic</c>-Stub erzeugt wird.</summary>
    public ImmutableList<TransitionCallContextCodeModel> TriggerTransitions { get; }
    /// <summary>Die Choices, deren <c>{Choice}Logic</c>-Stub erzeugt wird (§3.5).</summary>
    public ImmutableList<ChoiceCallContextCodeModel>     Choices            { get; }

    /// <summary>
    /// Baut das <c>{Task}WFS</c>-OneShot-Codemodell aus dem Semantic Model: <see cref="TaskCodeInfo"/>, Pfade
    /// (via <paramref name="pathProvider"/>) und die Init-/Exit-/Trigger-Transitionen samt Choices (über
    /// <see cref="CodeModelBuilderV2"/>) — die Quellen der zu erzeugenden Logic-Stubs.
    /// </summary>
    public static WfsCodeModelV2 FromTaskDefinition(ITaskDefinitionSymbol taskDefinition, IPathProvider pathProvider, GenerationOptions options) {

        if (taskDefinition == null) {
            throw new ArgumentNullException(nameof(taskDefinition));
        }

        var taskCodeInfo           = TaskCodeInfo.FromTaskDefinition(taskDefinition);
        var relativeSyntaxFileName = pathProvider.GetRelativePath(pathProvider.WfsFileName, pathProvider.SyntaxFileName);
        var taskResult             = ParameterCodeModel.TaskResult(taskDefinition);

        return new WfsCodeModelV2(
            taskCodeInfo          : taskCodeInfo,
            relativeSyntaxFileName: relativeSyntaxFileName,
            filePath              : pathProvider.WfsFileName,
            usingNamespaces       : GetUsingNamespaces(taskDefinition, taskCodeInfo).ToImmutableList(),
            initTransitions       : CodeModelBuilderV2.GetInitTransitions(taskDefinition, taskResult).ToImmutableList(),
            exitTransitions       : CodeModelBuilderV2.GetExitTransitions(taskDefinition, taskResult).ToImmutableList(),
            triggerTransitions    : CodeModelBuilderV2.GetTriggerTransitions(taskDefinition, taskResult).ToImmutableList(),
            choices               : CodeModelBuilderV2.GetChoices(taskDefinition, taskResult).ToImmutableList());
    }

    /// <summary>
    /// Sammelt die <c>using</c>-Namespaces der OneShot-Datei (System, IWFL-Namespace des Tasks, die
    /// Navigation-Engine-Namespaces und die im <c>.nav</c> deklarierten <c>using</c>s) und gibt sie sortiert zurück.
    /// </summary>
    static IEnumerable<string> GetUsingNamespaces(ITaskDefinitionSymbol taskDefinition, TaskCodeInfo taskCodeInfo) {
        // ReSharper disable once UseObjectOrCollectionInitializer
        var namespaces = new List<string>();

        namespaces.Add(typeof(int).Namespace);
        namespaces.Add(taskCodeInfo.IwflNamespace);
        namespaces.Add(CodeGenFacts.NavigationEngineIwflNamespace);
        namespaces.Add(CodeGenFacts.NavigationEngineWflNamespace);
        namespaces.AddRange(taskDefinition.CodeGenerationUnit.GetCodeUsingNamespaces());

        return namespaces.ToSortedNamespaces();
    }

}
