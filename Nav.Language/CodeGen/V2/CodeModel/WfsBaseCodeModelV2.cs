#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Codemodell der <c>{Task}WFSBase</c>-Familie der <b>Generation 2</b> (CallContext). Wie das
/// V1-Pendant beschreibt es die abstrakte Maschinerie-Basisklasse samt Begin-Wrapper-Feldern,
/// Konstruktoren und der partiellen Implementierungsklasse <c>{Task}WFS</c> — aber die Transitionen
/// tragen statt eines <c>switch(body)</c> je einen <see cref="CallContextCodeModel"/> und kollabieren
/// auf einen nackten <c>Unwrap()</c>-Aufruf (§3.3).
/// </summary>
sealed class WfsBaseCodeModelV2: FileGenerationCodeModel {

    WfsBaseCodeModelV2(TaskCodeInfo taskCodeInfo,
                       string relativeSyntaxFileName,
                       string filePath,
                       ImmutableList<string> usingNamespaces,
                       ParameterCodeModel taskResult,
                       ImmutableList<ParameterCodeModel> taskBegins,
                       ImmutableList<ParameterCodeModel> taskParameter,
                       ImmutableList<TransitionCallContextCodeModel> initTransitions,
                       ImmutableList<TransitionCallContextCodeModel> exitTransitions,
                       ImmutableList<TransitionCallContextCodeModel> triggerTransitions,
                       ImmutableList<ChoiceCallContextCodeModel> choices)
        : base(taskCodeInfo, relativeSyntaxFileName, filePath) {

        UsingNamespaces    = usingNamespaces    ?? throw new ArgumentNullException(nameof(usingNamespaces));
        TaskResult         = taskResult         ?? throw new ArgumentNullException(nameof(taskResult));
        TaskBegins         = taskBegins         ?? throw new ArgumentNullException(nameof(taskBegins));
        TaskParameter      = taskParameter      ?? throw new ArgumentNullException(nameof(taskParameter));
        InitTransitions    = initTransitions    ?? throw new ArgumentNullException(nameof(initTransitions));
        ExitTransitions    = exitTransitions    ?? throw new ArgumentNullException(nameof(exitTransitions));
        TriggerTransitions = triggerTransitions ?? throw new ArgumentNullException(nameof(triggerTransitions));
        Choices            = choices            ?? throw new ArgumentNullException(nameof(choices));

        // Je distinktem Trigger-View-TO eine BeforeTriggerLogic-Überladung (wie V1).
        ViewParameters = TriggerTransitions.Select(t => t.Parameters[0])
                                           .DistinctBy(p => p.ParameterType)
                                           .ToImmutableList();
    }

    /// <summary>Der Ziel-Namespace der generierten Klassen (durchgereicht aus <see cref="TaskCodeInfo"/>).</summary>
    public string WflNamespace        => Task.WflNamespace;
    /// <summary>Typname der abstrakten Basisklasse <c>{Task}WFSBase</c> (durchgereicht aus <see cref="TaskCodeInfo"/>).</summary>
    public string WfsBaseTypeName     => Task.WfsBaseTypeName;
    /// <summary>Typname der partiellen Implementierungsklasse <c>{Task}WFS</c> (durchgereicht aus <see cref="TaskCodeInfo"/>).</summary>
    public string WfsTypeName         => Task.WfsTypeName;
    /// <summary>Typname der Basisklasse von <c>{Task}WFSBase</c> (durchgereicht aus <see cref="TaskCodeInfo"/>).</summary>
    public string WfsBaseBaseTypeName => Task.WfsBaseBaseTypeName;

    /// <summary>Das Ergebnis des Tasks (Typ des <c>Exit</c>-Parameters, Generic der Task-Engine-Methoden).</summary>
    public ParameterCodeModel                             TaskResult         { get; }
    /// <summary>Die (sortierten) <c>using</c>-Namespaces der generierten Datei.</summary>
    public ImmutableList<string>                          UsingNamespaces    { get; }
    /// <summary>Die injizierten Begin-Wrapper der Sub-Tasks (<c>_x</c>-Felder, Konstruktorparameter).</summary>
    public ImmutableList<ParameterCodeModel>              TaskBegins         { get; }
    /// <summary>Die Task-Parameter (in <c>{Task}WFS</c> als Felder, in dessen Konstruktor).</summary>
    public ImmutableList<ParameterCodeModel>              TaskParameter      { get; }
    /// <summary>Die Init-Transitionen (<c>Begin</c>-Maschinerie + Logic + Context).</summary>
    public ImmutableList<TransitionCallContextCodeModel>  InitTransitions    { get; }
    /// <summary>Die Exit-Transitionen (<c>After{Node}</c>-Maschinerie der Sub-Task-Rücksprünge).</summary>
    public ImmutableList<TransitionCallContextCodeModel>  ExitTransitions    { get; }
    /// <summary>Die Trigger-Transitionen (Signal-<c>{Trigger}</c>-Maschinerie).</summary>
    public ImmutableList<TransitionCallContextCodeModel>  TriggerTransitions { get; }
    /// <summary>Die erreichbaren Choices als eigene Bausteine (§3.5).</summary>
    public ImmutableList<ChoiceCallContextCodeModel>      Choices            { get; }
    /// <summary>Die distinkten Trigger-View-TOs — je eines eine <c>BeforeTriggerLogic</c>-Überladung.</summary>
    public ImmutableList<ParameterCodeModel>              ViewParameters     { get; }

    /// <summary>
    /// Baut das <c>{Task}WFSBase</c>-Codemodell aus dem Semantic Model: <see cref="TaskCodeInfo"/>, Pfade
    /// (via <paramref name="pathProvider"/>), Task-Result/-Begins/-Parameter sowie die Init-/Exit-/Trigger-
    /// Transitionen und Choices (über <see cref="CodeModelBuilderV2"/>).
    /// </summary>
    public static WfsBaseCodeModelV2 FromTaskDefinition(ITaskDefinitionSymbol taskDefinition, IPathProvider pathProvider, GenerationOptions options) {

        if (taskDefinition == null) {
            throw new ArgumentNullException(nameof(taskDefinition));
        }

        var taskCodeInfo           = TaskCodeInfo.FromTaskDefinition(taskDefinition);
        var relativeSyntaxFileName = pathProvider.GetRelativePath(pathProvider.WfsBaseFileName, pathProvider.SyntaxFileName);

        var taskResult    = ParameterCodeModel.TaskResult(taskDefinition);
        var taskBegins    = CodeModelBuilder.GetTaskBeginParameter(taskDefinition);
        var taskParameter = CodeModelBuilder.GetTaskParameter(taskDefinition);

        return new WfsBaseCodeModelV2(
            taskCodeInfo          : taskCodeInfo,
            relativeSyntaxFileName: relativeSyntaxFileName,
            filePath              : pathProvider.WfsBaseFileName,
            usingNamespaces       : GetUsingNamespaces(taskDefinition, taskCodeInfo).ToImmutableList(),
            taskResult            : taskResult,
            taskBegins            : taskBegins.ToImmutableList(),
            taskParameter         : taskParameter.ToImmutableList(),
            initTransitions       : CodeModelBuilderV2.GetInitTransitions(taskDefinition, taskResult).ToImmutableList(),
            exitTransitions       : CodeModelBuilderV2.GetExitTransitions(taskDefinition, taskResult).ToImmutableList(),
            triggerTransitions    : CodeModelBuilderV2.GetTriggerTransitions(taskDefinition, taskResult).ToImmutableList(),
            choices               : CodeModelBuilderV2.GetChoices(taskDefinition, taskResult).ToImmutableList());
    }

    /// <summary>
    /// Sammelt die <c>using</c>-Namespaces der generierten Datei (System, IWFL-Namespace des Tasks, die
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
