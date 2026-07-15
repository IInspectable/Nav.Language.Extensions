#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Trägt die Datei mit der <b>Maschinerie-Basisklasse <c>{Task}WFSBase</c></b> und der partiellen
/// <b>Implementierungsklasse <c>{Task}WFS</c></b> (beide in <em>einer</em> generierten Datei). Konsument
/// ist der <see cref="WfsBaseEmitter"/>: aus <c>WFSBase</c> entstehen Felder und Konstruktoren (die
/// injizierten Sub-Task-Wrapper aus <see cref="TaskBegins"/>), die <c>Begin(…)</c>-, <c>After{Node}(…)</c>-
/// und <c>On{Trigger}(…)</c>-Methoden mit ihren <c>switch(body)</c>-Weichen (aus
/// <see cref="InitTransitions"/>/<see cref="ExitTransitions"/>/<see cref="TriggerTransitions"/>), die
/// <c>Begin{Node}(…)</c>-Wrapper (aus <see cref="BeginWrappers"/>) sowie <c>TaskResult(…)</c>; aus
/// <c>{Task}WFS</c> die zusätzlichen Task-Parameter-Felder (<see cref="TaskParameter"/>) und die
/// Basislisten-Referenz auf <c>I{Task}WFS</c>/<c>IBegin{Task}WFS</c>. Das zugehörige Regression-Beispiel
/// ist <c>TestWFSBase.generated.expected.cs</c>.
/// </summary>
sealed class WfsBaseCodeModel : FileGenerationCodeModel {

    WfsBaseCodeModel(TaskCodeInfo taskCodeInfo, 
                     string relativeSyntaxFileName, 
                     string filePath, 
                     ImmutableList<string> usingNamespaces, 
                     ParameterCodeModel taskResult, 
                     ImmutableList<ParameterCodeModel> taskBegins, 
                     ImmutableList<ParameterCodeModel> taskParameter,
                     ImmutableList<InitTransitionCodeModel> initTransitions,
                     ImmutableList<ExitTransitionCodeModel> exitTransitions,
                     ImmutableList<TriggerTransitionCodeModel> triggerTransitions,
                     ImmutableList<BeginWrapperCodeModel> beginWrappers) 
        : base(taskCodeInfo, relativeSyntaxFileName, filePath) {
            
        UsingNamespaces    = usingNamespaces    ?? throw new ArgumentNullException(nameof(usingNamespaces));
        TaskResult         = taskResult         ?? throw new ArgumentNullException(nameof(taskResult));
        TaskBegins         = taskBegins         ?? throw new ArgumentNullException(nameof(taskBegins));
        TaskParameter      = taskParameter      ?? throw new ArgumentNullException(nameof(taskParameter));
        InitTransitions    = initTransitions    ?? throw new ArgumentNullException(nameof(initTransitions));
        ExitTransitions    = exitTransitions    ?? throw new ArgumentNullException(nameof(exitTransitions));
        TriggerTransitions = triggerTransitions ?? throw new ArgumentNullException(nameof(triggerTransitions));
        BeginWrappers      = beginWrappers      ?? throw new ArgumentNullException(nameof(beginWrappers));

        ViewParameters     = TriggerTransitions.DistinctBy(ts => ts.ViewParameter.ParameterType).Select(ts => ts.ViewParameter).ToImmutableList();
    }
        
    /// <summary>Der (versionierbare) Namespace der Implementierungsklassen (<c>{ns}.WFL</c>); siehe <see cref="TaskCodeInfo.WflNamespace"/>.</summary>
    public string WflNamespace        => Task.WflNamespace;
    /// <summary>Der Name der generierten Basisklasse (<c>{Task}WFSBase</c>); siehe <see cref="TaskCodeInfo.WfsBaseTypeName"/>.</summary>
    public string WfsBaseTypeName     => Task.WfsBaseTypeName;
    /// <summary>Der Name der partiellen Implementierungsklasse (<c>{Task}WFS</c>); siehe <see cref="TaskCodeInfo.WfsTypeName"/>.</summary>
    public string WfsTypeName         => Task.WfsTypeName;
    /// <summary>Der Basistyp der Basisklasse (<c>{Task}WFSBase: <b>StandardWFS</b></c>) — aus <c>base</c>-Deklaration oder Default; siehe <see cref="TaskCodeInfo.WfsBaseBaseTypeName"/>.</summary>
    public string WfsBaseBaseTypeName => Task.WfsBaseBaseTypeName;

    /// <summary>Der Task-Ergebnistyp/-Parameter der <c>TaskResult(…)</c>-Methode (aus <c>[result]</c> bzw. Default); siehe <see cref="ParameterCodeModel.TaskResult(ITaskDefinitionSymbol)"/>.</summary>
    public ParameterCodeModel                        TaskResult         { get; }
    /// <summary>Die sortierten <c>using</c>-Namespaces im Kopf der Datei.</summary>
    public ImmutableList<string>                     UsingNamespaces    { get; }       
    /// <summary>Die injizierten Sub-Task-Begin-Wrapper als Konstruktor-Parameter/Felder (<c>IBegin{Sub}WFS {sub}</c>); siehe <see cref="CodeModelBuilder.GetTaskBeginParameter"/>.</summary>
    public ImmutableList<ParameterCodeModel>         TaskBegins         { get; }
    /// <summary>Die zusätzlichen Task-Parameter der <c>{Task}WFS</c>-Implementierungsklasse (aus <c>[params …]</c>); siehe <see cref="CodeModelBuilder.GetTaskParameter"/>.</summary>
    public ImmutableList<ParameterCodeModel>         TaskParameter      { get; }
    /// <summary>Die Init-Transitionen → <c>Begin(…)</c>-Methoden; siehe <see cref="CodeModelBuilder.GetInitTransitions"/>.</summary>
    public ImmutableList<InitTransitionCodeModel>    InitTransitions    { get; }
    /// <summary>Die Exit-Transitionen → <c>After{Node}(…)</c>-Rücksprünge; siehe <see cref="CodeModelBuilder.GetExitTransitions"/>.</summary>
    public ImmutableList<ExitTransitionCodeModel>    ExitTransitions    { get; }
    /// <summary>Die Trigger-Transitionen → <c>On{Trigger}(…)</c>-Methoden; siehe <see cref="CodeModelBuilder.GetTriggerTransitions"/>.</summary>
    public ImmutableList<TriggerTransitionCodeModel> TriggerTransitions { get; }
    /// <summary>Die Begin-Wrapper der erreichbaren Sub-Tasks → <c>Begin{Node}(…)</c>-Methoden; siehe <see cref="CodeModelBuilder.GetBeginWrappers"/>.</summary>
    public ImmutableList<BeginWrapperCodeModel>      BeginWrappers      { get; }

    /// <summary>
    /// Die distinct-View-Parameter über alle Trigger-Transitionen (je <c>ViewTO</c>-Typ einer) — Quelle
    /// der <c>protected virtual {ViewTO} BeforeTriggerLogic({ViewTO} to) =&gt; to;</c>-Hooks.
    /// </summary>
    public ImmutableList<ParameterCodeModel> ViewParameters { get; }

    /// <summary>
    /// Factory: baut das vollständige <see cref="WfsBaseCodeModel"/> aus dem Task-Symbol. Löst
    /// <see cref="TaskCodeInfo"/> und die Transitions-/Wrapper-/Parameterlisten über den
    /// <see cref="CodeModelBuilder"/> auf und bestimmt Datei- und relativen Syntaxpfad über den
    /// <paramref name="pathProvider"/>.
    /// </summary>
    public static WfsBaseCodeModel FromTaskDefinition(ITaskDefinitionSymbol taskDefinition, IPathProvider pathProvider, GenerationOptions options) {

        if (taskDefinition == null) {
            throw new ArgumentNullException(nameof(taskDefinition));
        }

        var taskCodeInfo           = TaskCodeInfo.FromTaskDefinition(taskDefinition);
        var relativeSyntaxFileName = pathProvider.GetRelativePath(pathProvider.WfsBaseFileName, pathProvider.SyntaxFileName);

        var taskResult         = ParameterCodeModel.TaskResult(taskDefinition);
        var usingNamespaces    = GetUsingNamespaces(taskDefinition, taskCodeInfo);
        var taskBegins         = CodeModelBuilder.GetTaskBeginParameter(taskDefinition);
        var taskParameter      = CodeModelBuilder.GetTaskParameter(taskDefinition);
        var initTransitions    = CodeModelBuilder.GetInitTransitions(taskDefinition   , taskCodeInfo);
        var exitTransitions    = CodeModelBuilder.GetExitTransitions(taskDefinition   , taskCodeInfo);
        var triggerTransitions = CodeModelBuilder.GetTriggerTransitions(taskDefinition, taskCodeInfo);
        var beginWrappers      = CodeModelBuilder.GetBeginWrappers(taskDefinition     , taskCodeInfo);

        return new WfsBaseCodeModel(
            taskCodeInfo          : taskCodeInfo,
            relativeSyntaxFileName: relativeSyntaxFileName,
            filePath              : pathProvider.WfsBaseFileName,
            usingNamespaces       : usingNamespaces.ToImmutableList(),
            taskResult            : taskResult,
            taskBegins            : taskBegins.ToImmutableList(),
            taskParameter         : taskParameter.ToImmutableList(),
            initTransitions       : initTransitions.ToImmutableList(),
            exitTransitions       : exitTransitions.ToImmutableList(),
            triggerTransitions    : triggerTransitions.ToImmutableList(),
            beginWrappers         : beginWrappers.ToImmutableList());
    }

    /// <summary>
    /// Stellt die <c>using</c>-Liste der Datei zusammen: <c>System</c>, den Interface-Namespace des Tasks
    /// (<see cref="TaskCodeInfo.IwflNamespace"/>), die Navigation-Engine-Namespaces und die im Nav-Code
    /// deklarierten <c>using</c>s — sortiert (<see cref="CodeGenerationUnitExtensions.ToSortedNamespaces"/>).
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