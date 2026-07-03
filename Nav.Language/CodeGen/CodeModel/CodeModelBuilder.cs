#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Pharmatechnik.Nav.Language.CodeGen;

sealed class CodeModelBuilder {

    public static IEnumerable<InitTransitionCodeModel> GetInitTransitions(ITaskDefinitionSymbol taskDefinition, TaskCodeInfo taskCodeInfo) {
        return taskDefinition.NodeDeclarations
                             .OfType<IInitNodeSymbol>()
                              // TODO Was ist mit Inits, die keine Outgoings haben, also eigentlich unbenutzt sind?
                             .Select(initNode => InitTransitionCodeModel.FromInitTransition(initNode, taskCodeInfo));
    }
        
    public static IEnumerable<ParameterCodeModel> GetTaskBeginParameter(ITaskDefinitionSymbol taskDefinition) {

        var usedTaskDeclarations = GetImplementedTaskNodes(taskDefinition)
                                  .Select(taskNode => taskNode.Declaration)
                                  .WhereNotNull()
                                  .Distinct()
                                  .ToImmutableList();
            
        var taskBegins = ParameterCodeModel.GetTaskBeginsAsParameter(usedTaskDeclarations)
                                           .OrderBy(p => p.ParameterName)
                                           .ToImmutableList();
        return taskBegins;
    }

    public static IEnumerable<BeginWrapperCodeModel> GetBeginWrappers(ITaskDefinitionSymbol taskDefinition, TaskCodeInfo taskCodeInfo) {
        return GetReachableTaskNodes(taskDefinition)
           .Select(taskNode => BeginWrapperCodeModel.FromTaskNode(taskNode, taskCodeInfo));
    }

    public static IEnumerable<ExitTransitionCodeModel> GetExitTransitions(ITaskDefinitionSymbol taskDefinition, TaskCodeInfo taskCodeInfo) {
        return GetReachableTaskNodes(taskDefinition)
              .Where(taskNode => !taskNode.CodeNotImplemented())
              .Select(taskNode => ExitTransitionCodeModel.FromTaskNode(taskNode, taskCodeInfo));
    }

    static ImmutableList<ITaskNodeSymbol> GetImplementedTaskNodes(ITaskDefinitionSymbol taskDefinition) {

        var relevantTaskNodes = taskDefinition.NodeDeclarations
                                              .OfType<ITaskNodeSymbol>()
                                              .Where(taskNode => !taskNode.CodeDoNotInject())
                                              .Where(taskNode => !taskNode.CodeNotImplemented())
                                              .Distinct();

        return relevantTaskNodes.ToImmutableList();
    }

    static ImmutableList<ITaskNodeSymbol> GetReachableTaskNodes(ITaskDefinitionSymbol taskDefinition) {

        var relevantTaskNodes = taskDefinition.NodeDeclarations
                                              .OfType<ITaskNodeSymbol>()
                                              .Where(taskNode => taskNode.IsReachable())
                                              .Distinct();

        return relevantTaskNodes.ToImmutableList();
    }

    public static IEnumerable<TriggerTransitionCodeModel> GetTriggerTransitions(ITaskDefinitionSymbol taskDefinition, TaskCodeInfo taskCodeInfo) {
        // Trigger Transitions sind per Defininition "used"
        return taskDefinition.TriggerTransitions
                             .SelectMany(triggerTransition => TriggerTransitionCodeModel.FromTriggerTransition(taskCodeInfo, triggerTransition))
                             .OrderBy(st => st.TriggerName.Length)
                             .ThenBy(st => st.TriggerName);
    }
        
    public static IEnumerable<string> GetCodeDeclarations(ITaskDefinitionSymbol taskDefinition) {
        var codeDeclaration = taskDefinition.Syntax.CodeDeclaration;
        if (codeDeclaration == null) {
            yield break;
        }

        foreach (var code in codeDeclaration.GetGetStringLiterals().Select(sl => sl.ToString().Trim('"'))) {
            yield return code;
        }
    }

    public static IEnumerable<ParameterCodeModel> GetTaskParameter(ITaskDefinitionSymbol taskDefinition) {
        var code          = GetTaskParameterSyntaxes();
        var taskParameter = ParameterCodeModel.FromParameterSyntaxes(code);
        return taskParameter;

        IEnumerable<ParameterSyntax> GetTaskParameterSyntaxes() {
            var paramList = taskDefinition.Syntax.CodeParamsDeclaration?.ParameterList;
            if(paramList == null) {
                yield break;
            }

            foreach(var p in paramList) {
                yield return p;
            }
        }
    }
    
}