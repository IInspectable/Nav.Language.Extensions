#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Bündelt für <b>einen Task-Knoten</b> die <c>Begin{Node}(…)</c>-Wrapper-Methoden, die der
/// <see cref="WfsBaseEmitter"/> im <c>{Task}WFSBase</c> erzeugt. Je Init-Anschluss des Ziel-Tasks
/// entsteht ein <see cref="TaskBeginCodeModel"/> (mehrere <c>Begin{Node}</c>-Überladungen bei mehreren
/// Inits); der Emitter macht daraus jeweils ein <c>return new TaskCall({Node}NodeName, () =&gt;
/// wfs.Begin(args));</c>. Der <c>{Node}NodeName</c>-Konstantenname leitet sich aus
/// <see cref="TaskNodeNamePascalcase"/> ab.
/// </summary>
class BeginWrapperCodeModel: CodeModel {

    public BeginWrapperCodeModel(string taskNodeName, ImmutableList<TaskBeginCodeModel> ctors) {

        TaskNodeName = taskNodeName;
        TaskBegins   = ctors ?? throw new ArgumentNullException(nameof(ctors));
    }

    /// <summary>Der Roh-Name des Task-Knotens (<c>{Node}</c>) — Wert der <c>{Node}NodeName</c>-Konstante.</summary>
    public string TaskNodeName           { get; }
    /// <summary>Der Pascalcase-Knotenname — Bestandteil von Methoden-/Konstantennamen (<c>Begin{Node}</c>, <c>{Node}NodeName</c>).</summary>
    public string TaskNodeNamePascalcase => TaskNodeName.ToPascalcase();

    /// <summary>Je Init-Anschluss des Ziel-Tasks ein <see cref="TaskBeginCodeModel"/> (eine <c>Begin{Node}</c>-Überladung).</summary>
    public ImmutableList<TaskBeginCodeModel> TaskBegins { get;}

    /// <summary>
    /// Fabrik: baut aus einem Task-Knoten die Begin-Wrapper. Für <c>[notimplemented]</c>-Tasks entsteht
    /// je Init ein Wrapper mit dem Default-Begin-Parameter (<see cref="CodeGenFacts.DefaultIwfsBaseType"/>)
    /// und leerem <c>TaskCall</c>; sonst mit dem konkreten <c>IBegin{Sub}WFS</c>-Parameter und den
    /// Init-Parametern. Wirft, wenn der Knoten keine Deklaration hat.
    /// </summary>
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

    /// <summary>Die im <c>[params …]</c> eines Init-Anschlusses deklarierten Parameter-Syntaxen (leer, wenn keine).</summary>
    static IEnumerable<ParameterSyntax> GetTaskParameterSyntaxes(IInitConnectionPointSymbol initConnectionPoint) {
        var parameterList = initConnectionPoint.Syntax.CodeParamsDeclaration?.ParameterList;
        return parameterList ?? Enumerable.Empty<ParameterSyntax>();
    }
}