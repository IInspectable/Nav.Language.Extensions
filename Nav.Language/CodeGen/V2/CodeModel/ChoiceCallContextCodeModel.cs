#region Using Directives

using System.Collections.Immutable;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Eine Choice als eigener V2-Baustein (§3.5): der geteilte <see cref="CallContextCodeModel"/> der
/// Choice-Ausgänge plus die abstrakte <c>{Choice}Logic</c>-Gegenstelle, in der der Nutzer die
/// Entscheidung <b>einmal</b> trifft — egal, wie viele Quellen auf die Choice zeigen. Anders als eine
/// <see cref="TransitionCallContextCodeModel"/> trägt eine Choice <b>keine</b> öffentliche
/// Maschinerie-Methode (kein <c>Begin</c>/<c>On{Trigger}</c>/<c>After{Node}</c>): sie wird nur über den
/// <c>{Choice}(…)</c>-Forward ihrer Quellen erreicht.
/// </summary>
/// <remarks>
/// Der Rückgabetyp der <c>Result.Unwrap()</c> (<see cref="CallContextCodeModel.CommandType"/>) ist
/// <c>IINIT_TASK</c>, sobald die Choice aus <b>irgendeiner</b> Init-Quelle erreichbar ist, sonst
/// <c>INavCommand</c> (§3.8/④). Choice→Choice bildet sich transitiv ab (der Choice-Context bekommt seinerseits
/// einen <c>{Choice}(…)</c>-Forward), die Anti-Bloat-Invariante bleibt gewahrt (jede <c>{Choice}Logic</c>
/// existiert genau einmal).
/// </remarks>
sealed class ChoiceCallContextCodeModel {

    ChoiceCallContextCodeModel(string choiceName,
                               string logicName,
                               ImmutableList<ParameterCodeModel> parameters,
                               CallContextCodeModel context) {
        ChoiceName = choiceName;
        LogicName  = logicName;
        Parameters = parameters;
        Context    = context;
    }

    /// <summary>
    /// Name des Choice-Knotens im <c>.nav</c> (unverändert, z.B. <c>Choice_Retry</c>) — trägt die
    /// <c>&lt;NavChoice&gt;</c>-Annotation für den C#→Nav-Rückweg.
    /// </summary>
    public string ChoiceName { get; }

    /// <summary>Name der abstrakten Entscheidungsmethode (<c>{Choice}Logic</c>).</summary>
    public string LogicName { get; }

    /// <summary>Die Choice-Parameter (<c>choice X [params …]</c>), die jede Quelle beim Forward übergibt.</summary>
    public ImmutableList<ParameterCodeModel> Parameters { get; }

    /// <summary>Der Call-Context der Choice — die Aufruffläche ihrer Ausgänge.</summary>
    public CallContextCodeModel Context { get; }

    /// <summary>
    /// Baut den V2-Baustein einer Choice aus ihrem <see cref="IChoiceNodeSymbol"/>: Name, <c>{Choice}Logic</c>,
    /// Choice-Parameter und den <see cref="CallContextCodeModel"/> ihrer Ausgänge. <paramref name="ownerTaskResult"/>
    /// ist das Ergebnis des umgebenden Tasks (für die <c>Exit</c>-Factory des Contexts). <paramref name="initReachable"/>
    /// steuert den Command-Typ (§3.8/④): <c>true</c> ⇒ <see cref="CallContextCodeModel.CommandType"/> ist
    /// <see cref="TransitionCallContextCodeModel.InitCommandType"/>, sonst
    /// <see cref="TransitionCallContextCodeModel.TransitionCommandType"/>.
    /// </summary>
    public static ChoiceCallContextCodeModel FromChoice(IChoiceNodeSymbol choiceNode,
                                                        ParameterCodeModel ownerTaskResult,
                                                        bool initReachable) {

        var namePascal  = choiceNode.Name.ToPascalcase();
        var logicName   = $"{namePascal}{CodeGenFacts.LogicMethodSuffix}";
        var commandType = initReachable
            ? TransitionCallContextCodeModel.InitCommandType
            : TransitionCallContextCodeModel.TransitionCommandType;

        var parameters = ParameterCodeModel.FromParameterSyntaxes(choiceNode.Syntax.CodeParamsDeclaration?.ParameterList)
                                           .ToImmutableList();

        var context = CallContextCodeModel.Build(
            contextTypeName: $"{namePascal}{CallContextCodeModel.ContextTypeSuffix}",
            commandType    : commandType,
            logicMethodName: logicName,
            directCalls    : choiceNode.Outgoings.GetDirectCalls(),
            ownerTaskResult: ownerTaskResult,
            declaresCancel : choiceNode.Outgoings.Any(edge => edge.TargetsCancel()));

        return new ChoiceCallContextCodeModel(
            choiceName: choiceNode.Name,
            logicName : logicName,
            parameters: parameters,
            context   : context);
    }

}
