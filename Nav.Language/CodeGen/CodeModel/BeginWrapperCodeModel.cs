#nullable enable

#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

class BeginWrapperCodeModel: CodeModel {

    public BeginWrapperCodeModel(string taskNodeName, ImmutableList<TaskBeginCodeModel> ctors) {

        TaskNodeName = taskNodeName;
        TaskBegins   = ctors ?? throw new ArgumentNullException(nameof(ctors));
    }

    public string TaskNodeName           { get; }
    public string TaskNodeNamePascalcase => TaskNodeName.ToPascalcase();

    public ImmutableList<TaskBeginCodeModel> TaskBegins { get;}

    public static BeginWrapperCodeModel FromTaskNode(ITaskNodeSymbol taskNode, TaskCodeInfo taskCodeInfo) {
            
        if (taskNode.Declaration == null) {
            throw new InvalidOperationException();
        }

        var taskBegins = new List<TaskBeginCodeModel>();

        foreach (var initConnectionPoint in taskNode.Declaration.Inits().WhereNotNull()) {

            var parameterSyntaxes = GetTaskParameterSyntaxes(initConnectionPoint);
            var taskParameter     = ParameterCodeModel.FromParameterSyntaxes(parameterSyntaxes);
               
            if (taskNode.Declaration.CodeNotImplemented) {

                var taskBegin = new TaskBeginCodeModel(
                    taskNodeName: taskNode.Name,
                    taskBeginParameter: new ParameterCodeModel(
                        parameterType : CodeGenFacts.DefaultIwfsBaseType,
                        parameterName : CodeGenFacts.TaskBeginParameterName),
                    taskParameter: taskParameter.ToImmutableList(),
                    notImplemented: true);

                taskBegins.Add(taskBegin);

            } else {
                var taskBegin = new TaskBeginCodeModel(
                    taskNodeName      : taskNode.Name, 
                    taskBeginParameter: ParameterCodeModel.GetTaskBeginAsParameter(taskNode.Declaration)
                                                          .WithParameterName(CodeGenFacts.TaskBeginParameterName), 
                    taskParameter     : taskParameter.ToImmutableList());

                taskBegins.Add(taskBegin);
            }                
        }
           
        return new BeginWrapperCodeModel(taskNode.Name, taskBegins.ToImmutableList());
    }

    static IEnumerable<ParameterSyntax> GetTaskParameterSyntaxes(IInitConnectionPointSymbol initConnectionPoint) {
        var parameterList = initConnectionPoint.Syntax.CodeParamsDeclaration?.ParameterList;
        return parameterList ?? Enumerable.Empty<ParameterSyntax>();
    }
}