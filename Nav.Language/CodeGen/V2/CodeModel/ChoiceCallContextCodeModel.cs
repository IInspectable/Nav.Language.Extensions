#region Using Directives

using System.Collections.Immutable;

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

    ChoiceCallContextCodeModel(string logicName,
                               ImmutableList<ParameterCodeModel> parameters,
                               CallContextCodeModel context) {
        LogicName  = logicName;
        Parameters = parameters;
        Context    = context;
    }

    /// <summary>Name der abstrakten Entscheidungsmethode (<c>{Choice}Logic</c>).</summary>
    public string LogicName { get; }

    /// <summary>Die Choice-Parameter (<c>choice X [params …]</c>), die jede Quelle beim Forward übergibt.</summary>
    public ImmutableList<ParameterCodeModel> Parameters { get; }

    /// <summary>Der Call-Context der Choice — die Aufruffläche ihrer Ausgänge.</summary>
    public CallContextCodeModel Context { get; }

    public static ChoiceCallContextCodeModel FromChoice(IChoiceNodeSymbol choiceNode,
                                                        ParameterCodeModel ownerTaskResult,
                                                        bool initReachable) {

        var namePascal  = choiceNode.Name.ToPascalcase();
        var commandType = initReachable
            ? TransitionCallContextCodeModel.InitCommandType
            : TransitionCallContextCodeModel.TransitionCommandType;

        var parameters = ParameterCodeModel.FromParameterSyntaxes(choiceNode.Syntax.CodeParamsDeclaration?.ParameterList)
                                           .ToImmutableList();

        var context = CallContextCodeModel.Build(
            contextTypeName: $"{namePascal}{CallContextCodeModel.ContextTypeSuffix}",
            commandType    : commandType,
            directCalls    : choiceNode.Outgoings.GetDirectCalls(),
            ownerTaskResult: ownerTaskResult);

        return new ChoiceCallContextCodeModel(
            logicName : $"{namePascal}{CodeGenFacts.LogicMethodSuffix}",
            parameters: parameters,
            context   : context);
    }

}
