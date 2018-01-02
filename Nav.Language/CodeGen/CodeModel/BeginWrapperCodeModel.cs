﻿#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen {

    class BeginWrapperCodeModel: CodeModel {

        public BeginWrapperCodeModel(string taskNodeName, ImmutableList<BeginWrapperCtor> ctors) {

            TaskNodeName = taskNodeName;
            Ctors        = ctors ?? throw new ArgumentNullException(nameof(ctors));
        }

        public string TaskNodeName { get; }
        public ImmutableList<BeginWrapperCtor> Ctors { get;}

        public static BeginWrapperCodeModel FromTaskNode(ITaskNodeSymbol taskNode, TaskCodeInfo taskCodeInfo) {
            
            if (taskNode.Declaration == null) {
                throw new InvalidOperationException();
            }

            // TODO Review bzgl. Pascalcasing => aus taskCodeInfo
            var taskNodeName = taskNode.Name.ToPascalcase();

            var ctors = new List<BeginWrapperCtor>();

            foreach (var initConnectionPoint in taskNode.Declaration.Inits().OfType<IInitConnectionPointSymbol>()) {

                var parameterSyntaxes = GetTaskParameterSyntaxes(initConnectionPoint);
                var taskParameter     = ParameterCodeModel.FromParameterSyntaxes(parameterSyntaxes);
               
                if (taskNode.Declaration.CodeNotImplemented) {

                    var ctor = new BeginWrapperCtor(
                        taskNodeName: taskNodeName,
                        taskBeginParameter: new ParameterCodeModel(
                            parameterType : CodeGenFacts.DefaultIwfsBaseType,
                            parameterName : CodeGenFacts.TaskBeginParameterName),
                        taskParameter: taskParameter.ToImmutableList(),
                        notImplemented: true);

                    ctors.Add(ctor);

                } else {
                    var ctor = new BeginWrapperCtor(
                        taskNodeName      : taskNodeName, 
                        taskBeginParameter: ParameterCodeModel.GetTaskBeginAsParameter(taskNode.Declaration)
                                                              .WithParameterName(CodeGenFacts.TaskBeginParameterName), 
                        taskParameter     : taskParameter.ToImmutableList());

                    ctors.Add(ctor);
                }                
            }
           
            return new BeginWrapperCodeModel(taskNodeName, ctors.ToImmutableList());
        }

        static IEnumerable<ParameterSyntax> GetTaskParameterSyntaxes(IInitConnectionPointSymbol initConnectionPoint) {
            var parameterList = initConnectionPoint.Syntax.CodeParamsDeclaration?.ParameterList;
            return parameterList ?? Enumerable.Empty<ParameterSyntax>();
        }
    }
}