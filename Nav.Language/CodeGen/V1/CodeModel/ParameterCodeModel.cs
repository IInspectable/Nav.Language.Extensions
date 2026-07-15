#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Ein einzelner Methoden-/Konstruktor-Parameter des erzeugten C#-Codes — schlicht ein Paar aus
/// <see cref="ParameterType"/> und <see cref="ParameterName"/>. Baustein der Signaturen und Argumentlisten
/// aller V1-CodeModels (Transitions-Parameter, Task-Begin-Wrapper, Task-Ergebnisse, Feld-Initialisierer); der
/// <see cref="WfsBaseEmitter"/> schreibt daraus <c>{ParameterType} {ParameterName}</c> bzw. den bloßen Namen als Argument.
/// </summary>
class ParameterCodeModel : CodeModel {

    public ParameterCodeModel(string? parameterType, string? parameterName) {
        ParameterType = parameterType ?? String.Empty;
        ParameterName = parameterName ?? String.Empty;
    }

    /// <summary>Der (C#-)Typ des Parameters.</summary>
    public virtual string ParameterType { get; }
    /// <summary>Der Name des Parameters.</summary>
    public virtual string ParameterName { get; }

    /// <summary>Liefert eine Kopie mit gleichem Typ, aber umbenannt — genutzt, um denselben Begin-Wrapper unter dem kanonischen Feld-/Parameternamen wiederzuverwenden.</summary>
    public ParameterCodeModel WithParameterName(string parameterName) {
        return new ParameterCodeModel(parameterType: ParameterType, parameterName: parameterName);
    }

    /// <summary>
    /// Der Ergebnis-Parameter eines Tasks aus Sicht seiner <b>Definition</b> (<c>[result]</c>-Deklaration).
    /// Fehlt die Angabe, gilt der Default (<see cref="CodeGenFacts.DefaultTaskResultType"/> /
    /// <see cref="CodeGenFacts.DefaultParamterName"/>).
    /// </summary>
    public static ParameterCodeModel TaskResult(ITaskDefinitionSymbol taskDefinition) {
        var codeParameter = taskDefinition.AsTaskDeclaration?.CodeTaskResult;
        var parameterType = CodeGenFacts.DefaultTaskResultType;
        var parameterName = CodeGenFacts.DefaultParamterName;
        if (codeParameter != null) {
            parameterType = codeParameter.ParameterType;
            parameterName = codeParameter.ParameterName == String.Empty ? CodeGenFacts.DefaultParamterName : codeParameter.ParameterName;
        }            
        return new ParameterCodeModel(parameterType, parameterName);
    }

    /// <summary>
    /// Der Ergebnis-Parameter eines Tasks aus Sicht seiner <b>Deklaration</b> — Grundlage des
    /// <c>After{Node}(result)</c>-Parameters und des generischen Task-Engine-Typarguments. Der Name ist stets
    /// <c>result</c>; fehlt die <c>[result]</c>-Angabe, wird auf <c>bool</c> zurückgefallen.
    /// </summary>
    public static ParameterCodeModel TaskResult(ITaskDeclarationSymbol? taskDeclaration) {
        var codeParameter = taskDeclaration?.CodeTaskResult;
        if (codeParameter == null) {
            // TODO New Error in Semantic Model: No result type defined with [result] - cannot use this task with exit edges.
            // Alternativ: #error pragma rausschreiben, und warning/Info in nav-file.
            return new ParameterCodeModel("bool", "result");
        }
        return new ParameterCodeModel(codeParameter.ParameterType, "result");
    }        

    /// <summary>
    /// Projiziert Parameter-Syntaxknoten (Init-/Trigger-/Task-Parameterlisten) in Parameter-Modelle. Unbenannte
    /// Parameter erhalten einen generierten Positionsnamen <c>p1</c>, <c>p2</c>, …
    /// </summary>
    public static IEnumerable<ParameterCodeModel> FromParameterSyntaxes(IEnumerable<ParameterSyntax>? parameters) {
        if (parameters == null) {
            yield break;
        }

        string GetParameterName(string name, ref int index) {
            return String.IsNullOrEmpty(name) ? $"p{index++}" : name;
        }

        int i = 1;
        foreach (var parameterSyntax in parameters) {
            yield return new ParameterCodeModel(
                parameterType: parameterSyntax.Type.ToString(),
                parameterName: GetParameterName(parameterSyntax.Identifier.ToString(), ref i));
        }
    }

    /// <summary>Bildet mehrere Task-Deklarationen auf ihre Begin-Wrapper-Parameter ab (siehe <see cref="GetTaskBeginAsParameter"/>).</summary>
    public static IEnumerable<ParameterCodeModel> GetTaskBeginsAsParameter(IEnumerable<ITaskDeclarationSymbol> taskDeclarations) {
        return taskDeclarations.Select(GetTaskBeginAsParameter);
    }

    /// <summary>
    /// Der Begin-Wrapper eines Sub-Tasks als Parameter: Typ ist das voll qualifizierte <c>IBegin{Task}WFS</c>-Interface
    /// (<see cref="TaskDeclarationCodeInfo.FullyQualifiedBeginInterfaceName"/>), Name der Task-Name in Camelcase — dies
    /// wird zum injizierten Konstruktor-Parameter und Backing-Feld. Für <c>[notimplemented]</c>-Tasks fällt der Typ auf
    /// <see cref="CodeGenFacts.DefaultIwfsBaseType"/> zurück (kein spezifisches Begin-Interface).
    /// </summary>
    public static ParameterCodeModel GetTaskBeginAsParameter(ITaskDeclarationSymbol taskDeclaration) {

        var codeInfo = TaskDeclarationCodeInfo.FromTaskDeclaration(taskDeclaration);

        if (taskDeclaration.CodeNotImplemented) {
            return new ParameterCodeModel(
                parameterType: CodeGenFacts.DefaultIwfsBaseType, 
                parameterName: CodeGenFacts.TaskBeginParameterName);
        }
            
        return new ParameterCodeModel(
            parameterType: codeInfo.FullyQualifiedBeginInterfaceName, 
            parameterName: codeInfo.Taskname.ToCamelcase());
    }       
}