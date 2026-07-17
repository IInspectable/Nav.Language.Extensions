#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Die Nav→C#-Anker einer Task-<b>Deklaration</b> (<c>taskref</c>): Task-Name und Namespace-Präfix,
/// daraus der voll qualifizierte Name des Begin-Interfaces <c>IBegin{Task}WFS</c> samt seiner
/// WFL-Ablage. Anders als <see cref="TaskCodeInfo"/> ist diese CodeInfo <b>vollständig versionsfrei</b>:
/// das Begin-Interface samt Ablage gehört zum generationsübergreifenden Schnittstellen-Vertrag, deshalb
/// bezieht sie ihre Bausteine ausschließlich aus <see cref="CodeGenInvariants"/>.
/// </summary>
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
    /// <summary>Der Name der deklarierten Task; Kern des <see cref="FullyQualifiedBeginInterfaceName"/>.</summary>
    public string Taskname                         { get; }
    /// <summary>Das Namespace-Präfix der Deklaration (<c>CodeNamespace</c>), unter das der WFL-Suffix gehängt wird.</summary>
    public string NamespacePräfix                  { get; }
    /// <summary>Namespace der Begin-Interface-Ablage (<c>{ns}.WFL</c>); invariant aus <see cref="CodeGenInvariants.WflNamespaceSuffix"/>.</summary>
    public string WflNamespace                     => BuildQualifiedName(NamespacePräfix, CodeGenInvariants.WflNamespaceSuffix);
    /// <summary>
    /// Voll qualifizierter Name des generierten Begin-Interfaces (<c>{ns}.WFL.IBegin{Task}WFS</c>) — der
    /// Nav→C#-Navigations-Anker der Task-Deklaration. Zusammengesetzt aus
    /// <see cref="CodeGenInvariants.BeginInterfacePrefix"/> und <see cref="CodeGenInvariants.InterfaceSuffix"/>.
    /// </summary>
    public string FullyQualifiedBeginInterfaceName => BuildQualifiedName(WflNamespace,    $"{CodeGenInvariants.BeginInterfacePrefix}{Taskname.ToPascalcase()}{CodeGenInvariants.InterfaceSuffix}");

    /// <summary>Factory: baut die <see cref="TaskDeclarationCodeInfo"/> zu einem Task-Deklarations-Symbol.</summary>
    public static TaskDeclarationCodeInfo FromTaskDeclaration(ITaskDeclarationSymbol taskDeclarationSymbol) {
        return new TaskDeclarationCodeInfo(taskDeclarationSymbol);
    }

    static string BuildQualifiedName(params string[] identifier) {
        return CodeGenFacts.BuildQualifiedName(identifier);
    }
}