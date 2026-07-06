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

    // Vollständig invariant: das Begin-Interface (IBegin{Task}WFS) samt seiner WFL-Ablage ist Teil des
    // generationsübergreifenden Schnittstellen-Vertrags — deshalb bezieht diese (bewusst versionsfreie)
    // CodeInfo ihre Bausteine aus CodeGenInvariants, nicht aus den versionierbaren Facts.
    public string Taskname                         { get; }
    public string NamespacePräfix                  { get; }
    public string WflNamespace                     => BuildQualifiedName(NamespacePräfix, CodeGenInvariants.WflNamespaceSuffix);
    public string FullyQualifiedBeginInterfaceName => BuildQualifiedName(WflNamespace,    $"{CodeGenInvariants.BeginInterfacePrefix}{Taskname.ToPascalcase()}{CodeGenInvariants.InterfaceSuffix}");

    public static TaskDeclarationCodeInfo FromTaskDeclaration(ITaskDeclarationSymbol taskDeclarationSymbol) {
        return new TaskDeclarationCodeInfo(taskDeclarationSymbol);
    }

    static string BuildQualifiedName(params string[] identifier) {
        return CodeGenFacts.BuildQualifiedName(identifier);
    }
}