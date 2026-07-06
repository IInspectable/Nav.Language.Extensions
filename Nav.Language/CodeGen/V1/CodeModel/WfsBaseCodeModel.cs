#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

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
        
    public string WflNamespace        => Task.WflNamespace;
    public string WfsBaseTypeName     => Task.WfsBaseTypeName;
    public string WfsTypeName         => Task.WfsTypeName;
    public string WfsBaseBaseTypeName => Task.WfsBaseBaseTypeName;

    public ParameterCodeModel                        TaskResult         { get; }
    public ImmutableList<string>                     UsingNamespaces    { get; }       
    public ImmutableList<ParameterCodeModel>         TaskBegins         { get; }
    public ImmutableList<ParameterCodeModel>         TaskParameter      { get; }
    public ImmutableList<InitTransitionCodeModel>    InitTransitions    { get; }
    public ImmutableList<ExitTransitionCodeModel>    ExitTransitions    { get; }
    public ImmutableList<TriggerTransitionCodeModel> TriggerTransitions { get; }
    public ImmutableList<BeginWrapperCodeModel>      BeginWrappers      { get; }

    public ImmutableList<ParameterCodeModel> ViewParameters { get; }

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