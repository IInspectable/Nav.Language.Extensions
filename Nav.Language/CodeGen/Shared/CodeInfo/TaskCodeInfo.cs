#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Text;

// ReSharper disable InconsistentNaming

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

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

    string        BaseNamespace         { get; }
    public string TaskName              { get; }
    public string WfsBaseBaseTypeName   { get; }
    public string IBeginWfsBaseTypeName { get; }

    public string TaskNamePascalcase        => TaskName.ToPascalcase();
    // Implementierungs-Namespace/-Typnamen: versionierbar (aus Facts).
    public string WflNamespace              => BuildQualifiedName(BaseNamespace, Facts.WflNamespaceSuffix);
    public string WfsBaseTypeName           => $"{TaskNamePascalcase}{Facts.WfsBaseClassSuffix}";
    public string WfsTypeName               => $"{TaskNamePascalcase}{Facts.WfsClassSuffix}";
    // Interface-Namensbildung/-Ablage: invariant (Schnittstellen-Vertrag). Das „WFS" in IWfsTypeName
    // ist der invariante Interface-Suffix, nicht der (gleichlautende) versionierbare Klassen-Suffix.
    public string IwflNamespace             => BuildQualifiedName(BaseNamespace, CodeGenInvariants.IwflNamespaceSuffix);
    public string IWfsTypeName              => $"{CodeGenInvariants.InterfacePrefix}{TaskNamePascalcase}{CodeGenInvariants.InterfaceSuffix}";
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