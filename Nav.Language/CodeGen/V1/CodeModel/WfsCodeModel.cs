#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Trägt die <b>einmalig angelegte Benutzer-Datei <c>{Task}WFS</c></b> — das per
/// <c>OverwritePolicy.Never</c> nur beim ersten Mal geschriebene, danach vom Benutzer gepflegte
/// Gegenstück zur generierten Basisklasse (Konsument: <c>WfsOneShotEmitter</c>). Trägt daher nur die
/// Rahmen-Informationen (Namespace, Typname, <c>using</c>s) sowie die Transitions-Listen, aus denen der
/// Emitter die anfänglichen <c>abstract</c>-Methoden-Rümpfe (<c>{Begin}Logic</c>/<c>{After}Logic</c>/
/// <c>{Trigger}Logic</c>) als Implementierungs-Vorlage ableitet. Nicht zu verwechseln mit
/// <see cref="WfsBaseCodeModel"/>, das die generierte Basis- <em>und</em> Implementierungsklasse in der
/// <c>*.generated</c>-Datei trägt.
/// </summary>
sealed class WfsCodeModel : FileGenerationCodeModel {

    public WfsCodeModel(
        TaskCodeInfo taskCodeInfo, 
        string relativeSyntaxFileName, 
        string filePath,
        ImmutableList<string> usingNamespaces,
        ImmutableList<InitTransitionCodeModel> initTransitions,
        ImmutableList<ExitTransitionCodeModel> exitTransitions,
        ImmutableList<TriggerTransitionCodeModel> triggerTransitions)
        : base(taskCodeInfo, relativeSyntaxFileName, filePath) {

        UsingNamespaces    = usingNamespaces    ?? throw new ArgumentNullException(nameof(usingNamespaces));
        InitTransitions    = initTransitions    ?? throw new ArgumentNullException(nameof(initTransitions));
        ExitTransitions    = exitTransitions    ?? throw new ArgumentNullException(nameof(exitTransitions));
        TriggerTransitions = triggerTransitions ?? throw new ArgumentNullException(nameof(triggerTransitions));
    }

    /// <summary>Der Namespace der Benutzer-Klasse (<c>{ns}.WFL</c>); siehe <see cref="TaskCodeInfo.WflNamespace"/>.</summary>
    public string WflNamespace => Task.WflNamespace;
    /// <summary>Der Name der partiellen Benutzer-Klasse (<c>{Task}WFS</c>); siehe <see cref="TaskCodeInfo.WfsTypeName"/>.</summary>
    public string WfsTypeName  => Task.WfsTypeName;

    /// <summary>Die sortierten <c>using</c>-Namespaces im Kopf der Datei.</summary>
    public ImmutableList<string>                     UsingNamespaces    { get; }
    /// <summary>Die Init-Transitionen → <c>{Begin}Logic(…)</c>-Stub-Overrides; siehe <see cref="CodeModelBuilder.GetInitTransitions"/>.</summary>
    public ImmutableList<InitTransitionCodeModel>    InitTransitions    { get; }
    /// <summary>Die Exit-Transitionen → <c>{After}Logic(…)</c>-Stub-Overrides; siehe <see cref="CodeModelBuilder.GetExitTransitions"/>.</summary>
    public ImmutableList<ExitTransitionCodeModel>    ExitTransitions    { get; }
    /// <summary>Die Trigger-Transitionen → <c>{Trigger}Logic(…)</c>-Stub-Overrides; siehe <see cref="CodeModelBuilder.GetTriggerTransitions"/>.</summary>
    public ImmutableList<TriggerTransitionCodeModel> TriggerTransitions { get; }

    /// <summary>
    /// Factory: baut das <see cref="WfsCodeModel"/> aus dem Task-Symbol (Namespaces, Transitions-Listen,
    /// Datei-/Syntaxpfad über den <paramref name="pathProvider"/>).
    /// </summary>
    public static WfsCodeModel FromTaskDefinition(ITaskDefinitionSymbol taskDefinition, IPathProvider pathProvider, GenerationOptions options) {

        if (taskDefinition == null) {
            throw new ArgumentNullException(nameof(taskDefinition));
        }

        var taskCodeInfo           = TaskCodeInfo.FromTaskDefinition(taskDefinition);
        var relativeSyntaxFileName = pathProvider.GetRelativePath(pathProvider.WfsFileName, pathProvider.SyntaxFileName);

        var usingNamespaces    = GetUsingNamespaces(taskDefinition, taskCodeInfo).ToImmutableList();
        var initTransitions    = CodeModelBuilder.GetInitTransitions(taskDefinition   , taskCodeInfo);
        var exitTransitions    = CodeModelBuilder.GetExitTransitions(taskDefinition   , taskCodeInfo);
        var triggerTransitions = CodeModelBuilder.GetTriggerTransitions(taskDefinition, taskCodeInfo);

        return new WfsCodeModel(
            taskCodeInfo          : taskCodeInfo,
            relativeSyntaxFileName: relativeSyntaxFileName,
            filePath              : pathProvider.WfsFileName,
            usingNamespaces       : usingNamespaces.ToImmutableList(),
            initTransitions       : initTransitions.ToImmutableList(),
            exitTransitions       : exitTransitions.ToImmutableList(),
            triggerTransitions    : triggerTransitions.ToImmutableList()
        );
    }

    /// <summary>
    /// Stellt die <c>using</c>-Liste der Benutzer-Datei zusammen (identisch zu
    /// <see cref="WfsBaseCodeModel"/>): <c>System</c>, Interface-Namespace des Tasks, Engine-Namespaces
    /// und die im Nav-Code deklarierten <c>using</c>s, sortiert.
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