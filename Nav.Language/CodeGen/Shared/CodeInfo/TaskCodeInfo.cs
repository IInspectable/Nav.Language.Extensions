#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Text;

// ReSharper disable InconsistentNaming

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Die zentrale Nav→C#-Namens-/Pfadschicht eines Tasks: leitet aus einem
/// <see cref="ITaskDefinitionSymbol"/> die vollständige C#-Namensalgebra der generierten Workflow-Typen
/// ab — Implementierungs-Klasse <c>{Task}WFS</c>, Basisklasse <c>{Task}WFSBase</c>, Interface
/// <c>I{Task}WFS</c>, deren Namespaces (<c>{ns}.WFL</c>/<c>{ns}.IWFL</c>) und die voll qualifizierten
/// Namen. Wurzel-Anker der abgeleiteten CodeInfos (<see cref="TaskInitCodeInfo"/>,
/// <see cref="TaskExitCodeInfo"/>, <see cref="SignalTriggerCodeInfo"/>, <see cref="ChoiceCodeInfo"/>),
/// die ihre versionierbaren Namensbausteine über <see cref="Facts"/> beziehen.
/// <para>
/// <b>Versionsweiche:</b> die Implementierungs-Namen (Klassen-/Namespace-Suffixe) sind versionierbar und
/// kommen aus <see cref="ICodeGenFacts"/>; die Interface-Namen und -Ablage sind
/// generationsübergreifend fix und kommen aus <see cref="CodeGenInvariants"/>.
/// </para>
/// </summary>
public sealed class TaskCodeInfo {

    TaskCodeInfo(ICodeGenFacts facts, string? taskName, string? baseNamespace, string? wfsBaseBaseClassName, string? iIBeginWfsBaseTypeName) {
        Facts                 = facts;
        TaskName              = taskName               ?? String.Empty;
        BaseNamespace         = baseNamespace          ?? String.Empty;
        WfsBaseBaseTypeName   = wfsBaseBaseClassName   ?? String.Empty;
        IBeginWfsBaseTypeName = iIBeginWfsBaseTypeName ?? String.Empty;
    }

    /// <summary>
    /// Die versionierbaren Codegen-Fakten der Generation dieses Tasks. Anker der Versionsweiche für
    /// die abgeleiteten CodeInfos (<c>TaskInit-</c>/<c>TaskExit-</c>/<c>SignalTriggerCodeInfo</c>), die
    /// ihre versionierbaren Namen über <c>ContainingTask.Facts</c> beziehen.
    /// </summary>
    internal ICodeGenFacts Facts { get; }

    /// <summary>Der Basis-Namespace des Tasks (<c>CodeNamespace</c>), unter den die WFL-/IWFL-Suffixe gehängt werden.</summary>
    string        BaseNamespace         { get; }
    /// <summary>Der rohe Task-Name aus dem Nav-Symbol; Basis von <see cref="TaskNamePascalcase"/> (leer, wenn abwesend).</summary>
    public string TaskName              { get; }
    /// <summary>
    /// Der Name der benutzerseitigen Basisklasse, von der <c>{Task}WFSBase</c> erbt (aus der
    /// <c>code base</c>-Deklaration; sonst der Default aus <see cref="CodeGenFacts.DefaultWfsBaseClass"/>).
    /// </summary>
    public string WfsBaseBaseTypeName   { get; }
    /// <summary>
    /// Der Name des benutzerseitigen Begin-Basis-Interfaces (aus der <c>code base</c>-Deklaration; sonst
    /// der Default aus <see cref="CodeGenFacts.DefaultIBeginWfsBaseType"/>).
    /// </summary>
    public string IBeginWfsBaseTypeName { get; }

    /// <summary>Der Task-Name in PascalCase — der Kern aller generierten Typnamen (<c>{Task}</c> in <c>{Task}WFS</c>).</summary>
    public string TaskNamePascalcase        => TaskName.ToPascalcase();
    // Implementierungs-Namespace/-Typnamen: versionierbar (aus Facts).
    /// <summary>Namespace der Implementierungstypen (<c>{ns}.WFL</c>); Suffix versionierbar aus <see cref="ICodeGenFacts.WflNamespaceSuffix"/>.</summary>
    public string WflNamespace              => BuildQualifiedName(BaseNamespace, Facts.WflNamespaceSuffix);
    /// <summary>Name der generierten Basisklasse (<c>{Task}WFSBase</c>); Suffix versionierbar aus <see cref="ICodeGenFacts.WfsBaseClassSuffix"/>.</summary>
    public string WfsBaseTypeName           => $"{TaskNamePascalcase}{Facts.WfsBaseClassSuffix}";
    /// <summary>Name der generierten Implementierungsklasse (<c>{Task}WFS</c>); Suffix versionierbar aus <see cref="ICodeGenFacts.WfsClassSuffix"/>.</summary>
    public string WfsTypeName               => $"{TaskNamePascalcase}{Facts.WfsClassSuffix}";
    // Interface-Namensbildung/-Ablage: invariant (Schnittstellen-Vertrag). Das „WFS" in IWfsTypeName
    // ist der invariante Interface-Suffix, nicht der (gleichlautende) versionierbare Klassen-Suffix.
    /// <summary>Namespace der <c>I{Task}WFS</c>-Interfaces (<c>{ns}.IWFL</c>); invariant aus <see cref="CodeGenInvariants.IwflNamespaceSuffix"/>.</summary>
    public string IwflNamespace             => BuildQualifiedName(BaseNamespace, CodeGenInvariants.IwflNamespaceSuffix);
    /// <summary>Name des generierten Interfaces (<c>I{Task}WFS</c>); invariant aus <see cref="CodeGenInvariants.InterfacePrefix"/>/<see cref="CodeGenInvariants.InterfaceSuffix"/>.</summary>
    public string IWfsTypeName              => $"{CodeGenInvariants.InterfacePrefix}{TaskNamePascalcase}{CodeGenInvariants.InterfaceSuffix}";
    /// <summary>Voll qualifizierter Name der Implementierungsklasse (<c>{ns}.WFL.{Task}WFS</c>).</summary>
    public string FullyQualifiedWfsName     => BuildQualifiedName(WflNamespace, WfsTypeName);

    /// <summary>
    /// Voll qualifizierter Name der generierten Basisklasse (<c>{ns}.WFL.{Task}WFSBase</c>).
    /// <b>Anker der Nav→C#-Navigation:</b> der <c>LocationFinder</c> löst über diesen FQN
    /// (<c>GetTypeByMetadataName</c>) den generierten Basistyp im Roslyn-Workspace auf und steigt von
    /// dort in die abgeleiteten Benutzer-Klassen ab. Erzeugt eine Generation keine solche separate
    /// Basisklasse mehr, trägt dieser Anker nicht mehr — dann greift nicht bloß ein anderer Name,
    /// sondern das Suchverfahren selbst muss versionsspezifisch werden.
    /// </summary>
    public string FullyQualifiedWfsBaseName => BuildQualifiedName(WflNamespace, WfsBaseTypeName);

    /// <summary>
    /// Fabrik: baut die <see cref="TaskCodeInfo"/> aus einer Task-Definition. Wählt die
    /// <see cref="ICodeGenFacts"/> nach der Sprach-Version der Datei
    /// (<see cref="NavCodeGenFacts.For(NavLanguageVersion)"/>) und fällt bei (noch) nicht unterstützter
    /// Version bewusst auf die Default-Generation zurück — CodeInfos entstehen auch für Anzeige und
    /// Navigation auf fehlerhaften Dateien.
    /// </summary>
    public static TaskCodeInfo FromTaskDefinition(ITaskDefinitionSymbol taskDefinition) {

        if (taskDefinition == null) {
            throw new ArgumentNullException(nameof(taskDefinition));
        }

        // Die versionierbaren Namen richten sich nach der Sprach-Version der Datei. Für eine (noch)
        // nicht unterstützte Version — CodeInfos entstehen auch für Anzeige/Navigation auf fehlerhaften
        // Dateien — fällt die Namensbildung bewusst auf die Default-Generation zurück.
        var version = taskDefinition.CodeGenerationUnit?.LanguageVersion ?? NavLanguageVersion.Default;
        var facts   = NavCodeGenFacts.For(version.IsSupported ? version : NavLanguageVersion.Default);

        var taskName              = taskDefinition.Name;
        var baseNamespace         = taskDefinition.CodeNamespace;
        var wfsBaseBaseClassName  = taskDefinition.Syntax.CodeBaseDeclaration?.WfsBaseType?.ToString()       ?? CodeGenFacts.DefaultWfsBaseClass;
        var iBeginWfsBaseTypeName = taskDefinition.Syntax.CodeBaseDeclaration?.IBeginWfsBaseType?.ToString() ?? CodeGenFacts.DefaultIBeginWfsBaseType;

        return new TaskCodeInfo(
            facts                 : facts,
            taskName              : taskName,
            baseNamespace         : baseNamespace,
            wfsBaseBaseClassName  : wfsBaseBaseClassName,
            iIBeginWfsBaseTypeName: iBeginWfsBaseTypeName);
    }

    static string BuildQualifiedName(params string[] identifier) {
        return CodeGenFacts.BuildQualifiedName(identifier);
    }

}