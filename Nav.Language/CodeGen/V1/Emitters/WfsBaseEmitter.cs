#region Using Directives

using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// CodeBuilder-Emitter der <c>{Task}WFSBase</c>-Familie — der technische Ersatz für <c>WFSBase.stg</c>.
/// Erzeugt in einer Datei die abstrakte Maschinerie-Basisklasse <c>{Task}WFSBase</c> (Felder,
/// Konstruktoren, Init-/Exit-/Trigger-Weichen samt <c>switch</c>-Blöcken, Begin-Wrapper) sowie die
/// zugehörige partielle Implementierungsklasse <c>{Task}WFS</c>.
/// </summary>
/// <remarks>
/// Anders als die Interface-Familien tragen Klassenname und Namespace hier <b>versionierbare</b>
/// Namen (<see cref="ICodeGenFacts"/> via <see cref="TaskCodeInfo"/>); nur die in der Basisliste der
/// Implementierungsklasse referenzierten Interface-Namen (<c>I{Task}WFS</c>, <c>IBegin{Task}WFS</c>)
/// bleiben invariant (<see cref="CodeGenInvariants"/>).
/// </remarks>
static class WfsBaseEmitter {

    public static string Emit(WfsBaseCodeModel model, CodeGeneratorContext context) {

        var cb    = new CodeBuilder();
        var facts = model.Task.Facts;

        EmitterCommon.WriteFileHeader(cb, context);
        EmitterCommon.WriteUsingDirectives(cb, model.UsingNamespaces);

        cb.Write($"""

                  namespace {model.WflNamespace} 
                  """);
        using (cb.Block()) {

            WriteBaseClass(cb, model, facts);

            cb.WriteLine();
            cb.WriteLine();

            WriteWfsClass(cb, model);
        }

        return cb.ToString();
    }

    // -- {Task}WFSBase: die abstrakte Maschinerie-Basisklasse -----------------------------------------

    static void WriteBaseClass(CodeBuilder cb, WfsBaseCodeModel model, ICodeGenFacts facts) {

        EmitterCommon.WriteTaskAnnotation(cb, model.RelativeSyntaxFileName, model.Task.TaskName);
        cb.Write($"public abstract partial class {model.WfsBaseTypeName}: {model.WfsBaseBaseTypeName} ");

        using (cb.Block()) {

            cb.WriteLine();

            if (model.BeginWrappers.Count > 0) {
                foreach (var beginWrapper in model.BeginWrappers) {
                    cb.WriteLine($"const string {beginWrapper.TaskNodeNamePascalcase}NodeName = \"{beginWrapper.TaskNodeName}\";");
                }

                cb.WriteLine();
            }

            if (model.TaskBegins.Count > 0) {
                foreach (var taskBegin in model.TaskBegins) {
                    cb.WriteLine($"readonly {taskBegin.ParameterType} {CodeGenFacts.FieldPrefix}{taskBegin.ParameterName} = default!;");
                }

                cb.WriteLine();
            }

            cb.WriteLine($"public {model.WfsBaseTypeName}({CodeGenFacts.NavigationEngineIwflNamespace}.IClientSideWFS clientSideWFS) {{}}");
            cb.WriteLine();

            WriteBaseConstructor(cb, model);
            cb.WriteLine();

            foreach (var viewParameter in model.ViewParameters) {
                cb.WriteLine($"protected virtual {viewParameter.ParameterType} {CodeGenFacts.BeforeTriggerLogicMethodName}({viewParameter.ParameterType} {viewParameter.ParameterName}) => {viewParameter.ParameterName};");
                cb.WriteLine();
            }

            foreach (var initTransition in model.InitTransitions) {
                WriteInitTransition(cb, initTransition, facts);
            }

            foreach (var exitTransition in model.ExitTransitions) {
                WriteExitTransition(cb, exitTransition, facts);
            }

            foreach (var triggerTransition in model.TriggerTransitions) {
                WriteTriggerTransition(cb, triggerTransition, facts);
            }

            foreach (var beginWrapper in model.BeginWrappers) {
                foreach (var taskBegin in beginWrapper.TaskBegins) {
                    WriteWrapperBeginMethod(cb, taskBegin, facts);
                }
            }

            cb.Write($"protected INavCommandBody TaskResult({model.TaskResult.ParameterType} {model.TaskResult.ParameterName}) ");
            using (cb.Block()) {
                cb.WriteLine($"return InternalTaskResult({model.TaskResult.ParameterName});");
            }

            cb.WriteLine();
            cb.WriteLine();
        }
    }

    static void WriteBaseConstructor(CodeBuilder cb, WfsBaseCodeModel model) {

        cb.Write($"public {model.WfsBaseTypeName}(");
        WriteParameterList(cb, model.TaskBegins);
        cb.Write(") ");

        using (cb.Block()) {
            foreach (var taskBegin in model.TaskBegins) {
                cb.WriteLine($"{CodeGenFacts.FieldPrefix}{taskBegin.ParameterName} = {taskBegin.ParameterName};");
            }
        }

        cb.WriteLine();
    }

    // -- {Task}WFS: die partielle Implementierungsklasse ----------------------------------------------

    static void WriteWfsClass(CodeBuilder cb, WfsBaseCodeModel model) {

        // I{Task}WFS/IBegin{Task}WFS sind invariante Schnittstellen (Grundsatz 3) → Namen aus den Invarianten.
        var iwfsName      = model.Task.IWfsTypeName;
        var iBeginWfsName = $"{CodeGenInvariants.BeginInterfacePrefix}{model.Task.TaskNamePascalcase}{CodeGenInvariants.InterfaceSuffix}";

        EmitterCommon.WriteTaskAnnotation(cb, model.RelativeSyntaxFileName, model.Task.TaskName);
        cb.Write($"public partial class {model.WfsTypeName}: {model.WfsBaseTypeName}, {iwfsName}, {iBeginWfsName} ");

        using (cb.Block()) {

            cb.WriteLine();

            if (model.TaskParameter.Count > 0) {
                foreach (var taskParameter in model.TaskParameter) {
                    cb.WriteLine($"readonly {taskParameter.ParameterType} {CodeGenFacts.FieldPrefix}{taskParameter.ParameterName} = default!;");
                }

                cb.WriteLine();
            }

            cb.WriteLine($"public {model.WfsTypeName}({CodeGenFacts.NavigationEngineIwflNamespace}.IClientSideWFS clientSideWFS): base(clientSideWFS) {{}}");
            cb.WriteLine();

            cb.Write($"public {model.WfsTypeName}(");
            WriteParameterList(cb, model.TaskBegins.Concat(model.TaskParameter));
            cb.WriteLine(")");

            cb.PushIndent();
            cb.Write(":base(");
            WriteExpressionListMultiline(cb, model.TaskBegins);
            cb.Write(")");
            cb.PopIndent();

            cb.Write(" ");
            using (cb.Block()) {
                foreach (var taskParameter in model.TaskParameter) {
                    cb.WriteLine($"{CodeGenFacts.FieldPrefix}{taskParameter.ParameterName} = {taskParameter.ParameterName};");
                }
            }

            cb.WriteLine();
        }
    }

    // -- Init-/Exit-/Trigger-Transitionen -------------------------------------------------------------

    static void WriteInitTransition(CodeBuilder cb, InitTransitionCodeModel initTransition, ICodeGenFacts facts) {

        var beginMethod = facts.BeginMethodPrefix;
        var logicMethod = $"{facts.BeginMethodPrefix}{facts.LogicMethodSuffix}";

        if (initTransition.GenerateAbstractMethod) {
            EmitterCommon.WriteNavInitAnnotation(cb, initTransition.NodeName);
            cb.Write($"public abstract IINIT_TASK {beginMethod}(");
            WriteParameterList(cb, initTransition.Parameter);
            cb.WriteLine(");");
            cb.WriteLine();
            return;
        }

        EmitterCommon.WriteNavInitAnnotation(cb, initTransition.NodeName);
        cb.Write($"public virtual IINIT_TASK {beginMethod}(");
        WriteParameterList(cb, initTransition.Parameter);
        cb.Write(") ");
        using (cb.Block()) {
            cb.Write($"var body = {logicMethod}(");
            WriteExpressionListInline(cb, initTransition.Parameter.Concat(initTransition.TaskBeginFields));
            cb.WriteLine(");");
            WriteTransitionCallBlock(cb, initTransition.ReachableCalls, logicMethod, facts);
        }

        cb.WriteLine();
        cb.WriteLine();

        EmitterCommon.WriteNavInitAnnotation(cb, initTransition.NodeName);
        cb.Write($"protected abstract INavCommandBody {logicMethod}(");
        WriteParameterList(cb, initTransition.Parameter.Concat(initTransition.TaskBegins));
        cb.WriteLine(");");
        cb.WriteLine();
    }

    static void WriteExitTransition(CodeBuilder cb, ExitTransitionCodeModel exitTransition, ICodeGenFacts facts) {

        var exitMethod  = $"{facts.ExitMethodPrefix}{exitTransition.NodeNamePascalcase}";
        var logicMethod = $"{exitMethod}{facts.LogicMethodSuffix}";

        if (exitTransition.GenerateAbstractMethod) {
            EmitterCommon.WriteNavExitAnnotation(cb, exitTransition.NodeName);
            cb.Write($"protected abstract INavCommand {logicMethod}(");
            WriteParameterList(cb, new[] { exitTransition.TaskResult });
            cb.WriteLine(");");
            cb.WriteLine();
            return;
        }

        EmitterCommon.WriteNavExitAnnotation(cb, exitTransition.NodeName);
        cb.Write($"protected virtual INavCommand {exitMethod}(");
        WriteParameterList(cb, new[] { exitTransition.TaskResult });
        cb.Write(") ");
        using (cb.Block()) {
            cb.Write($"var body = {logicMethod}(");
            WriteExpressionListInline(cb, new[] { exitTransition.TaskResult }.Concat(exitTransition.TaskBeginFields));
            cb.WriteLine(");");
            WriteTransitionCallBlock(cb, exitTransition.ReachableCalls, logicMethod, facts);
        }

        cb.WriteLine();
        cb.WriteLine();

        EmitterCommon.WriteNavExitAnnotation(cb, exitTransition.NodeName);
        cb.Write($"protected abstract INavCommandBody {logicMethod}(");
        WriteParameterList(cb, new[] { exitTransition.TaskResult }.Concat(exitTransition.TaskBegins));
        cb.WriteLine(");");
        cb.WriteLine();
    }

    static void WriteTriggerTransition(CodeBuilder cb, TriggerTransitionCodeModel triggerTransition, ICodeGenFacts facts) {

        var trigger     = triggerTransition.TriggerName;
        var logicMethod = $"{trigger}{facts.LogicMethodSuffix}";
        var viewParam   = triggerTransition.ViewParameter;

        EmitterCommon.WriteTriggerAnnotation(cb, trigger);
        cb.Write($"public virtual INavCommand {trigger}(");
        WriteParameterList(cb, new[] { viewParam });
        cb.Write(") ");
        using (cb.Block()) {
            cb.WriteLine($"{viewParam.ParameterName} = {CodeGenFacts.BeforeTriggerLogicMethodName}({viewParam.ParameterName});");
            cb.Write($"var body = {logicMethod}(");
            WriteExpressionListInline(cb, new[] { viewParam }.Concat(triggerTransition.TaskBeginFields));
            cb.WriteLine(");");
            WriteTransitionCallBlock(cb, triggerTransition.ReachableCalls, logicMethod, facts);
        }

        cb.WriteLine();
        cb.WriteLine();

        EmitterCommon.WriteTriggerAnnotation(cb, trigger);
        cb.Write($"protected abstract INavCommandBody {logicMethod}(");
        WriteParameterList(cb, new[] { viewParam }.Concat(triggerTransition.TaskBegins));
        cb.WriteLine(");");
        cb.WriteLine();
    }

    static void WriteWrapperBeginMethod(CodeBuilder cb, TaskBeginCodeModel taskBegin, ICodeGenFacts facts) {

        EmitterCommon.WriteInitCallAnnotation(cb, taskBegin.TaskBeginParameter.ParameterType);
        cb.Write($"protected INavCommandBody {facts.BeginMethodPrefix}{taskBegin.TaskNodeNamePascalcase}(");
        WriteParameterList(cb, new[] { taskBegin.TaskBeginParameter }.Concat(taskBegin.TaskParameter));
        cb.Write(") ");
        using (cb.Block()) {
            if (taskBegin.NotImplemented) {
                cb.WriteLine($"return new TaskCall({taskBegin.TaskNodeNamePascalcase}NodeName, null);");
            } else {
                cb.Write($"return new TaskCall({taskBegin.TaskNodeNamePascalcase}NodeName, () => {taskBegin.TaskBeginParameter.ParameterName}.{facts.BeginMethodPrefix}(");
                WriteExpressionListInline(cb, taskBegin.TaskParameter);
                cb.WriteLine("));");
            }
        }

        cb.WriteLine();
        cb.WriteLine();
    }

    // -- switch(body)-Weiche über die erreichbaren Calls ----------------------------------------------

    static void WriteTransitionCallBlock(CodeBuilder cb, IEnumerable<CallCodeModel> reachableCalls, string logicMethodName, ICodeGenFacts facts) {

        cb.Write("switch(body) ");
        using (cb.Block()) {
            foreach (var call in reachableCalls) {
                WriteCall(cb, call, facts);
            }

            cb.WriteLine("default:");
            using (cb.Indent()) {
                cb.WriteLine($"throw new InvalidOperationException(NavCommandBody.ComposeUnexpectedTransitionMessage(nameof({logicMethodName}), body));");
            }
        }

        cb.WriteLine();
    }

    static void WriteCall(CodeBuilder cb, CallCodeModel call, ICodeGenFacts facts) {

        switch (call.TemplateName) {
            case "cancel":
                cb.WriteLine("case CANCEL cancel:");
                using (cb.Indent()) {
                    cb.WriteLine("return cancel;");
                }

                break;
            case "goToExit":
                cb.WriteLine("case TASK_RESULT taskResult:");
                using (cb.Indent()) {
                    cb.WriteLine("return taskResult;");
                }

                break;
            case "goToEnd":
                cb.WriteLine("case END _:");
                using (cb.Indent()) {
                    cb.WriteLine("return EndNonModal();");
                }

                break;
            case "openModalTask":
                WriteTaskCall(cb, (TaskCallCodeModel) call, "OpenModalTask", generic: true, facts);
                break;
            case "startNonModalTask":
                WriteTaskCall(cb, (TaskCallCodeModel) call, "StartNonModalTask", generic: false, facts);
                break;
            case "gotoTask":
                WriteTaskCall(cb, (TaskCallCodeModel) call, "GotoTask", generic: true, facts);
                break;
            case "openModalGUI":
                WriteGuiCall(cb, call, "OpenModalGUI");
                break;
            case "startNonModalGUI":
                WriteGuiCall(cb, call, "StartNonModalGUI");
                break;
            case "gotoGUI":
                WriteGuiCall(cb, call, "GotoGUI");
                break;
        }
    }

    static void WriteTaskCall(CodeBuilder cb, TaskCallCodeModel call, string engineMethod, bool generic, ICodeGenFacts facts) {

        cb.WriteLine($"case TaskCall taskCall when taskCall.NodeName == {call.PascalcaseName}NodeName:");
        using (cb.Indent()) {
            if (call.NotImplemented) {
                cb.WriteLine($"throw new NotImplementedException(\"Task {call.Name} is specified as [notimplemented]\");");
            } else {
                var typeArgument = generic ? $"<{call.TaskResult.ParameterType}>" : "";
                cb.WriteLine($"return {engineMethod}{typeArgument}(taskCall.BeginWrapper, {facts.ExitMethodPrefix}{call.PascalcaseName});");
            }
        }

        // Der StringTemplate-Zweig für [notimplemented] hinterlässt eine strukturelle Leerzeile vor dem
        // nächsten case (nur der implementierte Zweig unterdrückt den Zeilenumbruch); bewusst reproduziert.
        if (call.NotImplemented) {
            cb.WriteLine();
        }
    }

    static void WriteGuiCall(CodeBuilder cb, CallCodeModel call, string engineMethod) {

        var toSuffix = CodeGenInvariants.ToClassNameSuffix;
        cb.WriteLine($"case {call.PascalcaseName}{toSuffix} {call.CamelcaseName}{toSuffix}:");
        using (cb.Indent()) {
            cb.WriteLine($"return {engineMethod}({call.CamelcaseName}{toSuffix});");
        }
    }

    // -- Parameter-/Ausdruckslisten -------------------------------------------------------------------

    /// <summary>Umbrochene, an der öffnenden Klammer ausgerichtete Parameterliste (<c>{Typ} {Name}</c>).</summary>
    static void WriteParameterList(CodeBuilder cb, IEnumerable<ParameterCodeModel> parameters) {
        cb.WriteAlignedJoin(parameters, p => cb.Write($"{p.ParameterType} {p.ParameterName}"), separator: $",{cb.NewLine}");
    }

    /// <summary>Umbrochene, ausgerichtete Argumentliste (nur die Namen) — für den <c>:base(…)</c>-Aufruf.</summary>
    static void WriteExpressionListMultiline(CodeBuilder cb, IEnumerable<ParameterCodeModel> parameters) {
        cb.WriteAlignedJoin(parameters, p => cb.Write(p.ParameterName), separator: $",{cb.NewLine}");
    }

    /// <summary>Einzeilige Argumentliste (nur die Namen, komma-getrennt) — für <c>{Method}(a, b, c)</c>.</summary>
    static void WriteExpressionListInline(CodeBuilder cb, IEnumerable<ParameterCodeModel> parameters) {
        cb.WriteJoin(parameters, p => cb.Write(p.ParameterName), separator: ", ");
    }

}
