#region Using Directives

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>Die Nav-Herkunft einer V2-Transition — bestimmt die Nav-Annotation im generierten Code.</summary>
enum TransitionAnnotationKind {

    Init,
    Exit,
    Trigger

}

/// <summary>
/// Eine Transition der V2-Maschinerie: die nackte <c>Unwrap()</c>-Maschinerie-Methode
/// (<c>Begin</c>/<c>On{Trigger}</c>/<c>After{Node}</c>), ihre abstrakte <c>…Logic</c>-Gegenstelle und
/// der zugehörige <see cref="CallContextCodeModel"/>. Der V1-<c>switch(body)</c> hat hier kein
/// Gegenstück mehr — die Maschinerie ist nur noch <c>…Logic(args, new {Context}(this)).Unwrap()</c>
/// (§3.3).
/// </summary>
/// <remarks>
/// Bei einer <c>[abstract]</c>-Quelle (<see cref="GenerateAbstractMachinery"/>) entfallen Logic und
/// Context: die Maschinerie-Methode selbst ist <c>abstract</c> und wird vom Nutzer voll implementiert
/// — wie in V1.
/// </remarks>
sealed class TransitionCallContextCodeModel {

    TransitionCallContextCodeModel(TransitionAnnotationKind annotationKind,
                                   string annotationName,
                                   string accessModifier,
                                   string returnType,
                                   string machineryName,
                                   ImmutableList<ParameterCodeModel> parameters,
                                   bool isTrigger,
                                   bool generateAbstractMachinery,
                                   string logicName,
                                   CallContextCodeModel? context) {

        AnnotationKind            = annotationKind;
        AnnotationName            = annotationName;
        AccessModifier            = accessModifier;
        ReturnType                = returnType;
        MachineryName             = machineryName;
        Parameters                = parameters;
        IsTrigger                 = isTrigger;
        GenerateAbstractMachinery = generateAbstractMachinery;
        LogicName                 = logicName;
        Context                   = context;
    }

    public TransitionAnnotationKind          AnnotationKind            { get; }
    public string                            AnnotationName            { get; }
    /// <summary>Zugriffsmodifikator der Maschinerie-Methode (<c>public</c> für Init/Trigger, <c>protected</c> für Exit).</summary>
    public string                            AccessModifier            { get; }
    public string                            ReturnType                { get; }
    public string                            MachineryName             { get; }
    public ImmutableList<ParameterCodeModel> Parameters                { get; }
    public bool                              IsTrigger                 { get; }
    public bool                              GenerateAbstractMachinery { get; }
    public string                            LogicName                 { get; }
    /// <summary>Der Call-Context der Quelle; <c>null</c> bei <see cref="GenerateAbstractMachinery"/>.</summary>
    public CallContextCodeModel?             Context                   { get; }

    // -- Init ----------------------------------------------------------------------------------------

    public static TransitionCallContextCodeModel FromInit(IInitNodeSymbol initNode, ParameterCodeModel taskResult) {

        var parameters = ParameterCodeModel.FromParameterSyntaxes(initNode.Syntax.CodeParamsDeclaration?.ParameterList)
                                           .ToImmutableList();

        var generateAbstract = initNode.CodeGenerateAbstractMethod();
        var contextTypeName  = $"{initNode.Name.ToPascalcase()}{ContextTypeSuffix}";

        var context = generateAbstract
            ? null
            : CallContextCodeModel.Build(
                contextTypeName: contextTypeName,
                commandType    : InitCommandType,
                directCalls    : initNode.Outgoings.GetDirectCalls(),
                ownerTaskResult: taskResult);

        return new TransitionCallContextCodeModel(
            annotationKind           : TransitionAnnotationKind.Init,
            annotationName           : initNode.Name,
            accessModifier           : "public",
            returnType               : InitCommandType,
            machineryName            : CodeGenFacts.BeginMethodPrefix,
            parameters               : parameters,
            isTrigger                : false,
            generateAbstractMachinery: generateAbstract,
            logicName                : $"{CodeGenFacts.BeginMethodPrefix}{CodeGenFacts.LogicMethodSuffix}",
            context                  : context);
    }

    // -- Exit (Rücksprung eines Sub-Tasks) -----------------------------------------------------------

    public static TransitionCallContextCodeModel FromExit(ITaskNodeSymbol taskNode, ParameterCodeModel taskResult) {

        var machineryName    = $"{CodeGenFacts.ExitMethodPrefix}{taskNode.Name.ToPascalcase()}";
        var subTaskResult    = ParameterCodeModel.TaskResult(taskNode.Declaration);
        var generateAbstract = taskNode.CodeGenerateAbstractMethod();
        var contextTypeName  = $"{machineryName}{ContextTypeSuffix}";

        var context = generateAbstract
            ? null
            : CallContextCodeModel.Build(
                contextTypeName: contextTypeName,
                commandType    : TransitionCommandType,
                directCalls    : taskNode.Outgoings.GetDirectCalls(),
                ownerTaskResult: taskResult);

        return new TransitionCallContextCodeModel(
            annotationKind           : TransitionAnnotationKind.Exit,
            annotationName           : taskNode.Name,
            accessModifier           : "protected",
            returnType               : TransitionCommandType,
            machineryName            : machineryName,
            parameters               : ImmutableList.Create(subTaskResult),
            isTrigger                : false,
            generateAbstractMachinery: generateAbstract,
            logicName                : $"{machineryName}{CodeGenFacts.LogicMethodSuffix}",
            context                  : context);
    }

    // -- Trigger -------------------------------------------------------------------------------------

    public static IEnumerable<TransitionCallContextCodeModel> FromTrigger(ITriggerTransition triggerTransition, ParameterCodeModel taskResult) {

        foreach (var signalTrigger in triggerTransition.Triggers.OfType<ISignalTriggerSymbol>()) {

            var triggerCodeInfo = SignalTriggerCodeInfo.FromSignalTrigger(signalTrigger);
            var viewParameter   = new ParameterCodeModel(triggerCodeInfo.TOClassName, CodeGenFacts.ToParamtername);
            var triggerName     = triggerCodeInfo.TriggerName;
            var contextTypeName = $"{triggerName}{ContextTypeSuffix}";

            var context = CallContextCodeModel.Build(
                contextTypeName: contextTypeName,
                commandType    : TransitionCommandType,
                directCalls    : new IEdge[] { triggerTransition }.GetDirectCalls(),
                ownerTaskResult: taskResult);

            yield return new TransitionCallContextCodeModel(
                annotationKind           : TransitionAnnotationKind.Trigger,
                annotationName           : triggerName,
                accessModifier           : "public",
                returnType               : TransitionCommandType,
                machineryName            : triggerName,
                parameters               : ImmutableList.Create(viewParameter),
                isTrigger                : true,
                generateAbstractMachinery: false,
                logicName                : $"{triggerName}{CodeGenFacts.LogicMethodSuffix}",
                context                  : context);
        }
    }

    /// <summary>Rückgabetyp/Command-Typ einer Init-Transition (nur <c>IINIT_TASK</c>-Kommandos zulässig).</summary>
    public const string InitCommandType = "IINIT_TASK";

    /// <summary>Rückgabetyp/Command-Typ einer Trigger-/Exit-Transition.</summary>
    public const string TransitionCommandType = "INavCommand";

    const string ContextTypeSuffix = CallContextCodeModel.ContextTypeSuffix;

}
