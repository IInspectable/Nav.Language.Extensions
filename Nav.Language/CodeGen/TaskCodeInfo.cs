#nullable enable

#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Text;

// ReSharper disable InconsistentNaming

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

public sealed class TaskCodeInfo {

    TaskCodeInfo(string? taskName, string? baseNamespace, string? wfsBaseBaseClassName, string? iIBeginWfsBaseTypeName) {
        TaskName              = taskName               ?? String.Empty;
        BaseNamespace         = baseNamespace          ?? String.Empty;
        WfsBaseBaseTypeName   = wfsBaseBaseClassName   ?? String.Empty;
        IBeginWfsBaseTypeName = iIBeginWfsBaseTypeName ?? String.Empty;
    }

    string        BaseNamespace         { get; }
    public string TaskName              { get; }
    public string WfsBaseBaseTypeName   { get; }
    public string IBeginWfsBaseTypeName { get; }

    public string TaskNamePascalcase        => TaskName.ToPascalcase();
    public string WflNamespace              => BuildQualifiedName(BaseNamespace, CodeGenFacts.WflNamespaceSuffix);
    public string IwflNamespace             => BuildQualifiedName(BaseNamespace, CodeGenFacts.IwflNamespaceSuffix);
    public string WfsBaseTypeName           => $"{TaskNamePascalcase}{CodeGenFacts.WfsBaseClassSuffix}";
    public string WfsTypeName               => $"{TaskNamePascalcase}{CodeGenFacts.WfsClassSuffix}";
    public string IWfsTypeName              => $"{CodeGenFacts.InterfacePrefix}{TaskNamePascalcase}{CodeGenFacts.WfsClassSuffix}";
    public string FullyQualifiedWfsName     => BuildQualifiedName(WflNamespace, WfsTypeName);
    public string FullyQualifiedWfsBaseName => BuildQualifiedName(WflNamespace, WfsBaseTypeName);

    public static TaskCodeInfo FromTaskDefinition(ITaskDefinitionSymbol taskDefinition) {

        if (taskDefinition == null) {
            throw new ArgumentNullException(nameof(taskDefinition));
        }

        var taskName              = taskDefinition.Name;
        var baseNamespace         = taskDefinition.CodeNamespace;
        var wfsBaseBaseClassName  = taskDefinition.Syntax.CodeBaseDeclaration?.WfsBaseType?.ToString()       ?? CodeGenFacts.DefaultWfsBaseClass;
        var iBeginWfsBaseTypeName = taskDefinition.Syntax.CodeBaseDeclaration?.IBeginWfsBaseType?.ToString() ?? CodeGenFacts.DefaultIBeginWfsBaseType;

        return new TaskCodeInfo(
            taskName              : taskName,
            baseNamespace         : baseNamespace,
            wfsBaseBaseClassName  : wfsBaseBaseClassName,
            iIBeginWfsBaseTypeName: iBeginWfsBaseTypeName);
    }

    static string BuildQualifiedName(params string[] identifier) {
        return CodeGenFacts.BuildQualifiedName(identifier);
    }

}