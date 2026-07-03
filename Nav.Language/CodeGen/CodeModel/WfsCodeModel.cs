#nullable enable

#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

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

    public string WflNamespace => Task.WflNamespace;
    public string WfsTypeName  => Task.WfsTypeName;

    public ImmutableList<string>                     UsingNamespaces    { get; }
    public ImmutableList<InitTransitionCodeModel>    InitTransitions    { get; }
    public ImmutableList<ExitTransitionCodeModel>    ExitTransitions    { get; }
    public ImmutableList<TriggerTransitionCodeModel> TriggerTransitions { get; }

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