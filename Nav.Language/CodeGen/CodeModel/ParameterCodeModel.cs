#nullable enable

#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

class ParameterCodeModel : CodeModel {

    public ParameterCodeModel(string? parameterType, string? parameterName) {
        ParameterType = parameterType ?? String.Empty;
        ParameterName = parameterName ?? String.Empty;
    }

    public virtual string ParameterType { get; }
    public virtual string ParameterName { get; }

    public ParameterCodeModel WithParameterName(string parameterName) {
        return new ParameterCodeModel(parameterType: ParameterType, parameterName: parameterName);
    }

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

    public static ParameterCodeModel TaskResult(ITaskDeclarationSymbol? taskDeclaration) {
        var codeParameter = taskDeclaration?.CodeTaskResult;
        if (codeParameter == null) {
            // TODO New Error in Semantic Model: No result type defined with [result] - cannot use this task with exit edges.
            // Alternativ: #error pragma rausschreiben, und warning/Info in nav-file.
            return new ParameterCodeModel("bool", "result");
        }
        return new ParameterCodeModel(codeParameter.ParameterType, "result");
    }        

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

    public static IEnumerable<ParameterCodeModel> GetTaskBeginsAsParameter(IEnumerable<ITaskDeclarationSymbol> taskDeclarations) {
        return taskDeclarations.Select(GetTaskBeginAsParameter);
    }

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