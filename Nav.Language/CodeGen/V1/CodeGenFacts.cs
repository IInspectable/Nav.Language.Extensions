#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Die V1-Codegen-Fakten als gewöhnliche, handgeschriebene C#-Konstanten. Bis zur Ablösung von
/// StringTemplate wurden diese Werte aus <c>CodeGenFacts.stg</c> nach <c>CodeGenFacts.generated.cs</c>
/// exportiert; seit dem Wegfall des ST-Sonderwegs stehen sie direkt hier. Sie bleiben die einzige
/// Wertequelle für die Namensalgebra der Generation 1 — <see cref="NavCodeGenFacts"/> (V1) delegiert
/// an sie, und Navigations-/Anzeige-Konsumenten (u.a. <c>AnnotationReader</c>) lesen die invarianten
/// Bausteine hier.
/// </summary>
public static class CodeGenFacts {

    /// <summary>Präfix eines C#-Interface-Namens (<c>I</c>) — Basis von <c>I{Task}WFS</c>. Invariant (vgl. <see cref="CodeGenInvariants.InterfacePrefix"/>).</summary>
    public const string InterfacePrefix              = "I";
    /// <summary>
    /// Default-Basis-Interface eines <c>I{Task}WFS</c> (<c>IWFService</c>), wenn die <c>.nav</c>-Datei keine
    /// eigene <c>base</c>-Deklaration liefert. Fallback u.a. in <c>IWfsCodeModel</c>, <c>ParameterCodeModel</c>
    /// und <c>BeginWrapperCodeModel</c>.
    /// </summary>
    public const string DefaultIwfsBaseType          = "IWFService";
    /// <summary>
    /// Default-Basis-Interface eines <c>IBegin{Task}WFS</c> (<c>IBeginWFService</c>), wenn keine
    /// <c>base</c>-Deklaration vorliegt. Fallback in <c>TaskCodeInfo</c>.
    /// </summary>
    public const string DefaultIBeginWfsBaseType     = "IBeginWFService";
    /// <summary>Suffix der abstrakten Logic-Methoden (<c>Logic</c> in <c>{…}Logic</c>). Versionierbar (vgl. <see cref="ICodeGenFacts.LogicMethodSuffix"/>).</summary>
    public const string LogicMethodSuffix            = "Logic";
    /// <summary>
    /// Name der generierten <c>protected virtual</c>-Vorverarbeitungsmethode für TO-View-Parameter
    /// (<c>BeforeTriggerLogic</c>), die der WFS-Basis-Emitter vor jedem Trigger-Aufruf einschiebt.
    /// </summary>
    public const string BeforeTriggerLogicMethodName = "BeforeTriggerLogic";
    /// <summary>Suffix der TO-Typnamen (<c>TO</c> in <c>{View}TO</c>). Invariant (vgl. <see cref="CodeGenInvariants.ToClassNameSuffix"/>).</summary>
    public const string ToClassNameSuffix            = "TO";
    /// <summary>Parametername des TO-View-Parameters in Trigger-Signaturen (<c>to</c> in <c>{Trigger}(XyzTO to)</c>).</summary>
    public const string ToParamtername               = "to";
    /// <summary>Namespace-Suffix der Implementierungs-Ablage (<c>WFL</c> in <c>{ns}.WFL</c>). Versionierbar (vgl. <see cref="ICodeGenFacts.WflNamespaceSuffix"/>).</summary>
    public const string WflNamespaceSuffix           = "WFL";
    /// <summary>Namespace-Suffix, unter dem die <c>I{Task}WFS</c>-Interfaces liegen (<c>IWFL</c>). Invariant (vgl. <see cref="CodeGenInvariants.IwflNamespaceSuffix"/>).</summary>
    public const string IwflNamespaceSuffix          = "IWFL";
    /// <summary>Suffix der generierten Basisklasse (<c>WFSBase</c> in <c>{Task}WFSBase</c>). Versionierbar (vgl. <see cref="ICodeGenFacts.WfsBaseClassSuffix"/>).</summary>
    public const string WfsBaseClassSuffix           = "WFSBase";
    /// <summary>Suffix des Implementierungs-Klassennamens (<c>WFS</c> in <c>{Task}WFS</c>). Versionierbar (vgl. <see cref="ICodeGenFacts.WfsClassSuffix"/>).</summary>
    public const string WfsClassSuffix               = "WFS";
    /// <summary>Parametername des Task-Begin-Parameters (<c>wfs</c>) in den generierten Begin-Wrapper-Signaturen.</summary>
    public const string TaskBeginParameterName       = "wfs";
    /// <summary>Präfix der generierten Begin-Methoden (<c>Begin</c> in <c>Begin{Node}</c>). Versionierbar (vgl. <see cref="ICodeGenFacts.BeginMethodPrefix"/>).</summary>
    public const string BeginMethodPrefix            = "Begin";
    /// <summary>Präfix der generierten Exit-Methoden (<c>After</c> in <c>After{Node}</c>). Versionierbar (vgl. <see cref="ICodeGenFacts.ExitMethodPrefix"/>).</summary>
    public const string ExitMethodPrefix             = "After";
    /// <summary>Präfix des Begin-Interfaces (<c>IBegin</c>) — Basis von <c>IBegin{Task}WFS</c>. Invariant (vgl. <see cref="CodeGenInvariants.BeginInterfacePrefix"/>).</summary>
    public const string BeginInterfacePrefix         = "IBegin";
    /// <summary>Voll qualifizierter Framework-Namespace der <c>IWFL</c>-Basistypen; wird als <c>using</c>-Direktive in den generierten Code aufgenommen.</summary>
    public const string NavigationEngineIwflNamespace = "Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL";
    /// <summary>Voll qualifizierter Framework-Namespace der <c>WFL</c>-Basistypen; wird als <c>using</c>-Direktive in den generierten Code aufgenommen.</summary>
    public const string NavigationEngineWflNamespace  = "Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL";
    /// <summary>Sentinel-Platzhalter für einen nicht auflösbaren Namespace (<c>__UNKNOWN__NAMESPACE__</c>).</summary>
    public const string UnknownNamespace             = "__UNKNOWN__NAMESPACE__";
    /// <summary>Default-Parametername (<c>par</c>), wenn eine Nav-Parameterdeklaration keinen eigenen Namen trägt (<c>ParameterCodeModel</c>).</summary>
    public const string DefaultParamterName          = "par";
    /// <summary>Default-Ergebnistyp einer Task ohne explizit deklariertes Result (<c>bool</c>; <c>ParameterCodeModel</c>).</summary>
    public const string DefaultTaskResultType        = "bool";
    /// <summary>Default-Basisklasse der generierten <c>{Task}WFSBase</c>, wenn keine <c>base</c>-Deklaration vorliegt (<c>BaseWFService</c>; <c>TaskCodeInfo</c>).</summary>
    public const string DefaultWfsBaseClass          = "BaseWFService";
    /// <summary>Präfix generierter Felder (<c>_</c> in <c>_{Parameter}</c>).</summary>
    public const string FieldPrefix                  = "_";

    /// <summary>Gemeinsames Präfix aller Nav-Annotation-Tags (<c>Nav</c>). Invariant (vgl. <see cref="CodeGenInvariants.AnnotationTagPrefix"/>).</summary>
    public const string AnnotationTagPrefix      = "Nav";
    /// <summary>Tag, das die generierte Datei mit ihrer <c>.nav</c>-Quelle verknüpft (<c>NavFile</c>). Invariant (vgl. <see cref="CodeGenInvariants.AnnotationTagNavFile"/>).</summary>
    public const string AnnotationTagNavFile       = "NavFile";
    /// <summary>Tag am generierten Task-Typ (<c>NavTask</c>). Invariant (vgl. <see cref="CodeGenInvariants.AnnotationTagNavTask"/>).</summary>
    public const string AnnotationTagNavTask       = "NavTask";
    /// <summary>Tag an einer generierten Trigger-Methode (<c>NavTrigger</c>). Invariant (vgl. <see cref="CodeGenInvariants.AnnotationTagNavTrigger"/>).</summary>
    public const string AnnotationTagNavTrigger    = "NavTrigger";
    /// <summary>Tag an der generierten Init-Methode (<c>NavInit</c>). Invariant (vgl. <see cref="CodeGenInvariants.AnnotationTagNavInit"/>).</summary>
    public const string AnnotationTagNavInit       = "NavInit";
    /// <summary>Tag an einer generierten Exit-Methode (<c>NavExit</c>). Invariant (vgl. <see cref="CodeGenInvariants.AnnotationTagNavExit"/>).</summary>
    public const string AnnotationTagNavExit       = "NavExit";
    /// <summary>Tag an einer generierten <c>{Choice}Logic</c>-Methode (<c>NavChoice</c>). Invariant (vgl. <see cref="CodeGenInvariants.AnnotationTagNavChoice"/>).</summary>
    public const string AnnotationTagNavChoice     = "NavChoice";
    /// <summary>Tag an einem generierten Init-Aufruf (<c>NavInitCall</c>). Invariant (vgl. <see cref="CodeGenInvariants.AnnotationTagNavInitCall"/>).</summary>
    public const string AnnotationTagNavInitCall   = "NavInitCall";
    /// <summary>Tag an einem generierten <c>{Choice}(…)</c>-Forward (<c>NavChoiceCall</c>). Invariant (vgl. <see cref="CodeGenInvariants.AnnotationTagNavChoiceCall"/>).</summary>
    public const string AnnotationTagNavChoiceCall = "NavChoiceCall";

    /// <summary>
    /// Fügt die übergebenen Bezeichner-Teile durch <c>.</c> getrennt zu einem qualifizierten Namen
    /// (Namespace bzw. Typname) zusammen und überspringt dabei leere und <c>null</c>-Teile. Sind alle
    /// Teile leer, ist das Ergebnis <see cref="String.Empty"/>. Basis der <c>WflNamespace</c>-/
    /// <c>IwflNamespace</c>-Bildung in <c>TaskCodeInfo</c> und <c>TaskDeclarationCodeInfo</c>.
    /// </summary>
    /// <param name="identifier">Die Namensteile in Reihenfolge; leere/<c>null</c>-Teile werden ausgelassen.</param>
    /// <returns>Der punkt-separierte qualifizierte Name, oder <see cref="String.Empty"/>, wenn kein Teil übrig bleibt.</returns>
    internal static string BuildQualifiedName(params string[] identifier) {

        // Heiß auf dem Codegen-Pfad (voll qualifizierte Namespaces/Typnamen, je Task-Begin neu gebildet):
        // bewusst ohne LINQ/List-Zwischenschritt. Der häufigste Fall — alle Teile nicht leer — geht direkt
        // über die schnelle String.Join(string[])-Überladung; nur wenn tatsächlich leere Teile zu
        // überspringen sind, wird ein passend dimensioniertes Zwischen-Array gefüllt.
        var nonEmpty = 0;
        foreach (var part in identifier) {
            if (!String.IsNullOrEmpty(part)) {
                nonEmpty++;
            }
        }

        if (nonEmpty == 0) {
            return String.Empty;
        }

        if (nonEmpty == identifier.Length) {
            return String.Join(".", identifier);
        }

        var parts = new string[nonEmpty];
        var i     = 0;
        foreach (var part in identifier) {
            if (!String.IsNullOrEmpty(part)) {
                parts[i++] = part;
            }
        }

        return String.Join(".", parts);
    }

}
