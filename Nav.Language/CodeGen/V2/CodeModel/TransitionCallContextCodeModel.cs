#region Using Directives

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>Die Nav-Herkunft einer V2-Transition — bestimmt die Nav-Annotation im generierten Code.</summary>
enum TransitionAnnotationKind {

    /// <summary>Init-Transition — trägt die <c>&lt;NavInit&gt;</c>-Annotation.</summary>
    Init,
    /// <summary>Exit-Transition (Rücksprung eines Sub-Tasks) — trägt die <c>&lt;NavExit&gt;</c>-Annotation.</summary>
    Exit,
    /// <summary>Trigger-Transition (Signal) — trägt die <c>&lt;Trigger&gt;</c>-Annotation.</summary>
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

    /// <summary>Die Nav-Herkunft der Transition — bestimmt die Nav-Annotation (siehe <see cref="TransitionAnnotationKind"/>).</summary>
    public TransitionAnnotationKind          AnnotationKind            { get; }
    /// <summary>Der Name des Quell-Knotens bzw. Triggers (Inhalt der Nav-Annotation).</summary>
    public string                            AnnotationName            { get; }
    /// <summary>Zugriffsmodifikator der Maschinerie-Methode (<c>public</c> für Init/Trigger, <c>protected</c> für Exit).</summary>
    public string                            AccessModifier            { get; }
    /// <summary>Rückgabetyp der Maschinerie-Methode (<see cref="InitCommandType"/> bei Init, sonst <see cref="TransitionCommandType"/>).</summary>
    public string                            ReturnType                { get; }
    /// <summary>Name der Maschinerie-Methode (<c>Begin</c>, <c>After{Node}</c> bzw. der Trigger-Name).</summary>
    public string                            MachineryName             { get; }
    /// <summary>Die Parameter der Maschinerie-Methode (Init-Parameter, Sub-Task-Result bzw. das Trigger-View-TO).</summary>
    public ImmutableList<ParameterCodeModel> Parameters                { get; }
    /// <summary>Ob es eine Trigger-Transition ist (dann läuft vor dem <c>Unwrap()</c> die <c>BeforeTriggerLogic</c>).</summary>
    public bool                              IsTrigger                 { get; }
    /// <summary>Ob die Quelle <c>[abstract]</c> ist — dann ist die Maschinerie-Methode selbst <c>abstract</c> und Logic/Context entfallen.</summary>
    public bool                              GenerateAbstractMachinery { get; }
    /// <summary>Name der abstrakten <c>…Logic</c>-Gegenstelle, deren Override der Nutzer implementiert.</summary>
    public string                            LogicName                 { get; }
    /// <summary>Der Call-Context der Quelle; <c>null</c> bei <see cref="GenerateAbstractMachinery"/>.</summary>
    public CallContextCodeModel?             Context                   { get; }

    // -- Init ----------------------------------------------------------------------------------------

    /// <summary>
    /// Baut die Init-Transition aus einem <see cref="IInitNodeSymbol"/>: die <c>public IINIT_TASK Begin(…)</c>-Methode
    /// mit den Init-Parametern. Ist der Init <c>[abstract]</c>, entfällt der Context (die <c>Begin</c>-Methode wird
    /// abstrakt); <paramref name="taskResult"/> ist das Ergebnis des umgebenden Tasks (für die <c>Exit</c>-Factory des Contexts).
    /// </summary>
    public static TransitionCallContextCodeModel FromInit(IInitNodeSymbol initNode, ParameterCodeModel taskResult) {

        var parameters = ParameterCodeModel.FromParameterSyntaxes(initNode.Syntax.CodeParamsDeclaration?.ParameterList)
                                           .ToImmutableList();

        var generateAbstract = initNode.CodeGenerateAbstractMethod();
        var contextTypeName  = $"{initNode.Name.ToPascalcase()}{ContextTypeSuffix}";
        var logicName        = $"{CodeGenFacts.BeginMethodPrefix}{CodeGenFacts.LogicMethodSuffix}";

        var context = generateAbstract
            ? null
            : CallContextCodeModel.Build(
                contextTypeName: contextTypeName,
                commandType    : InitCommandType,
                logicMethodName: logicName,
                directCalls    : initNode.Outgoings.GetDirectCalls(),
                ownerTaskResult: taskResult,
                declaresCancel : initNode.Outgoings.Any(edge => edge.TargetsCancel()));

        return new TransitionCallContextCodeModel(
            annotationKind           : TransitionAnnotationKind.Init,
            annotationName           : initNode.Name,
            accessModifier           : "public",
            returnType               : InitCommandType,
            machineryName            : CodeGenFacts.BeginMethodPrefix,
            parameters               : parameters,
            isTrigger                : false,
            generateAbstractMachinery: generateAbstract,
            logicName                : logicName,
            context                  : context);
    }

    // -- Exit (Rücksprung eines Sub-Tasks) -----------------------------------------------------------

    /// <summary>
    /// Baut die Exit-Transition aus einem <see cref="ITaskNodeSymbol"/> (Rücksprung eines Sub-Tasks): die
    /// <c>protected INavCommand After{Node}({SubTaskResult})</c>-Methode. Ist der Task-Knoten <c>[abstract]</c>,
    /// entfällt der Context; <paramref name="taskResult"/> ist das Ergebnis des umgebenden Tasks (für die <c>Exit</c>-Factory).
    /// </summary>
    public static TransitionCallContextCodeModel FromExit(ITaskNodeSymbol taskNode, ParameterCodeModel taskResult) {

        var machineryName    = $"{CodeGenFacts.ExitMethodPrefix}{taskNode.Name.ToPascalcase()}";
        var subTaskResult    = ParameterCodeModel.TaskResult(taskNode.Declaration);
        var generateAbstract = taskNode.CodeGenerateAbstractMethod();
        var contextTypeName  = $"{machineryName}{ContextTypeSuffix}";
        var logicName        = $"{machineryName}{CodeGenFacts.LogicMethodSuffix}";

        var context = generateAbstract
            ? null
            : CallContextCodeModel.Build(
                contextTypeName: contextTypeName,
                commandType    : TransitionCommandType,
                logicMethodName: logicName,
                directCalls    : taskNode.Outgoings.GetDirectCalls(),
                ownerTaskResult: taskResult,
                declaresCancel : taskNode.Outgoings.Any(edge => edge.TargetsCancel()));

        return new TransitionCallContextCodeModel(
            annotationKind           : TransitionAnnotationKind.Exit,
            annotationName           : taskNode.Name,
            accessModifier           : "protected",
            returnType               : TransitionCommandType,
            machineryName            : machineryName,
            parameters               : ImmutableList.Create(subTaskResult),
            isTrigger                : false,
            generateAbstractMachinery: generateAbstract,
            logicName                : logicName,
            context                  : context);
    }

    // -- Trigger -------------------------------------------------------------------------------------

    /// <summary>
    /// Baut je Signal-Trigger einer <see cref="ITriggerTransition"/> eine Trigger-Transition: die
    /// <c>public INavCommand {Trigger}({View}TO)</c>-Methode. Ein Trigger ist nie <c>[abstract]</c> und
    /// bekommt immer einen Context; <paramref name="taskResult"/> ist das Ergebnis des umgebenden Tasks
    /// (für die <c>Exit</c>-Factory des Contexts).
    /// </summary>
    public static IEnumerable<TransitionCallContextCodeModel> FromTrigger(ITriggerTransition triggerTransition, ParameterCodeModel taskResult) {

        foreach (var signalTrigger in triggerTransition.Triggers.OfType<ISignalTriggerSymbol>()) {

            var triggerCodeInfo = SignalTriggerCodeInfo.FromSignalTrigger(signalTrigger);
            var viewParameter   = new ParameterCodeModel(triggerCodeInfo.TOClassName, CodeGenFacts.ToParamtername);
            var triggerName     = triggerCodeInfo.TriggerName;
            var contextTypeName = $"{triggerName}{ContextTypeSuffix}";
            var logicName       = $"{triggerName}{CodeGenFacts.LogicMethodSuffix}";

            var context = CallContextCodeModel.Build(
                contextTypeName: contextTypeName,
                commandType    : TransitionCommandType,
                logicMethodName: logicName,
                directCalls    : new IEdge[] { triggerTransition }.GetDirectCalls(),
                ownerTaskResult: taskResult,
                declaresCancel : triggerTransition.TargetsCancel());

            yield return new TransitionCallContextCodeModel(
                annotationKind           : TransitionAnnotationKind.Trigger,
                annotationName           : triggerName,
                accessModifier           : "public",
                returnType               : TransitionCommandType,
                machineryName            : triggerName,
                parameters               : ImmutableList.Create(viewParameter),
                isTrigger                : true,
                generateAbstractMachinery: false,
                logicName                : logicName,
                context                  : context);
        }
    }

    /// <summary>Rückgabetyp/Command-Typ einer Init-Transition (nur <c>IINIT_TASK</c>-Kommandos zulässig).</summary>
    public const string InitCommandType = "IINIT_TASK";

    /// <summary>Rückgabetyp/Command-Typ einer Trigger-/Exit-Transition.</summary>
    public const string TransitionCommandType = "INavCommand";

    const string ContextTypeSuffix = CallContextCodeModel.ContextTypeSuffix;

}
