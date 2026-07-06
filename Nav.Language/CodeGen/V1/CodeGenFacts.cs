#region Using Directives

using System;
using System.Linq;

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

    public const string InterfacePrefix              = "I";
    public const string DefaultIwfsBaseType          = "IWFService";
    public const string DefaultIBeginWfsBaseType     = "IBeginWFService";
    public const string LogicMethodSuffix            = "Logic";
    public const string BeforeTriggerLogicMethodName = "BeforeTriggerLogic";
    public const string ToClassNameSuffix            = "TO";
    public const string ToParamtername               = "to";
    public const string WflNamespaceSuffix           = "WFL";
    public const string IwflNamespaceSuffix          = "IWFL";
    public const string WfsBaseClassSuffix           = "WFSBase";
    public const string WfsClassSuffix               = "WFS";
    public const string TaskBeginParameterName       = "wfs";
    public const string BeginMethodPrefix            = "Begin";
    public const string ExitMethodPrefix             = "After";
    public const string BeginInterfacePrefix         = "IBegin";
    public const string NavigationEngineIwflNamespace = "Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL";
    public const string NavigationEngineWflNamespace  = "Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL";
    public const string UnknownNamespace             = "__UNKNOWN__NAMESPACE__";
    public const string DefaultParamterName          = "par";
    public const string DefaultTaskResultType        = "bool";
    public const string DefaultWfsBaseClass          = "BaseWFService";
    public const string FieldPrefix                  = "_";

    public const string AnnotationTagPrefix      = "Nav";
    public const string AnnotationTagNavFile     = "NavFile";
    public const string AnnotationTagNavTask     = "NavTask";
    public const string AnnotationTagNavTrigger  = "NavTrigger";
    public const string AnnotationTagNavInit     = "NavInit";
    public const string AnnotationTagNavExit     = "NavExit";
    public const string AnnotationTagNavInitCall = "NavInitCall";

    internal static string BuildQualifiedName(params string[] identifier) {
        var parts = identifier.Where(part => !String.IsNullOrEmpty(part)).ToList();

        if (!parts.Any()) {
            return String.Empty;
        }

        return String.Join(".", parts);
    }

}
