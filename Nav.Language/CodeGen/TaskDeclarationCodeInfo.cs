#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

public sealed class TaskDeclarationCodeInfo {

    TaskDeclarationCodeInfo(ITaskDeclarationSymbol taskDeclarationSymbol) {

        if (taskDeclarationSymbol == null) {
            throw new ArgumentNullException(nameof(taskDeclarationSymbol));
        }

        Taskname        = taskDeclarationSymbol.Name;
        NamespacePräfix = taskDeclarationSymbol.CodeNamespace;
    }

    public string Taskname                         { get; }
    public string NamespacePräfix                  { get; }
    public string WflNamespace                     => BuildQualifiedName(NamespacePräfix, CodeGenFacts.WflNamespaceSuffix);
    public string FullyQualifiedBeginInterfaceName => BuildQualifiedName(WflNamespace,    $"{CodeGenFacts.BeginInterfacePrefix}{Taskname.ToPascalcase()}{CodeGenFacts.WfsClassSuffix}");

    public static TaskDeclarationCodeInfo FromTaskDeclaration(ITaskDeclarationSymbol taskDeclarationSymbol) {
        return new TaskDeclarationCodeInfo(taskDeclarationSymbol);
    }

    static string BuildQualifiedName(params string[] identifier) {
        return CodeGenFacts.BuildQualifiedName(identifier);
    }
}