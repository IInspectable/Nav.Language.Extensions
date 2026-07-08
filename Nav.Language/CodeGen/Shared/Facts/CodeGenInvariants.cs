namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Die <b>generationsübergreifend unveränderlichen</b> Codegen-Fakten. Anders als die
/// versionierbaren Werte (Implementierungs-Klassennamen, Logic-Methoden, Default-Typen …) dürfen
/// diese Konstanten über <i>keine</i> Nav-Sprachversion hinweg abweichen — sie bilden zwei per
/// Grundsatz eingefrorene Verträge:
/// <list type="bullet">
///   <item>
///     die Bausteine der <b>Interface-Namensbildung</b> (<c>I{Task}WFS</c> /
///     <c>IBegin{Task}WFS</c>) samt deren Ablage und der TO-Typnamen, die in ihren Signaturen
///     auftauchen — die Schnittstelle zum Workflow-Code;
///   </item>
///   <item>
///     die <b>Annotation-Tags</b> der XML-Doku im generierten Code, über die der
///     <c>AnnotationReader</c> aus C# wieder in die Nav-Datei zurückfindet.
///   </item>
/// </list>
/// Weil diese Werte unveränderlich sind, ist ihre <c>const</c>-Einkompilierung auch in fremde
/// Assemblies (<c>Nav.Language.CodeAnalysis</c>, <c>Nav.Language.ExtensionShared</c>) legal.
/// </summary>
public static class CodeGenInvariants {

    // -- Interface-Namensbildung ---------------------------------------------------------------------------

    /// <summary>Präfix eines C#-Interface-Namens (<c>I</c>) — Basis von <c>I{Task}WFS</c>.</summary>
    public const string InterfacePrefix = "I";

    /// <summary>Präfix des Begin-Interfaces (<c>IBegin</c>) — Basis von <c>IBegin{Task}WFS</c>.</summary>
    public const string BeginInterfacePrefix = "IBegin";

    /// <summary>
    /// Suffix der generierten Interface-Namen (<c>WFS</c> in <c>I{Task}WFS</c>/<c>IBegin{Task}WFS</c>).
    /// Bewusst getrennt vom gleichlautenden, aber versionierbaren Klassen-Suffix des
    /// Implementierungstyps <c>{Task}WFS</c> (heute <c>CodeGenFacts.WfsClassSuffix</c>): der
    /// Interface-Vertrag ist invariant, der Implementierungs-Klassenname darf je Generation abweichen.
    /// </summary>
    public const string InterfaceSuffix = "WFS";

    /// <summary>Namespace-Suffix, unter dem die <c>I{Task}WFS</c>-Interfaces liegen (<c>IWFL</c>).</summary>
    public const string IwflNamespaceSuffix = "IWFL";

    /// <summary>
    /// Namespace-Suffix, unter dem die <c>IBegin{Task}WFS</c>-Interfaces liegen (<c>WFL</c>).
    /// Bewusst getrennt vom gleichlautenden, aber versionierbaren Implementierungs-Namespace-Suffix
    /// (heute <c>CodeGenFacts.WflNamespaceSuffix</c> bzw. <see cref="ICodeGenFacts.WflNamespaceSuffix"/>):
    /// die Ablage des Begin-Interfaces gehört zum invarianten Schnittstellen-Vertrag, der
    /// Implementierungs-Namespace der <c>{Task}WFS</c>-Typen darf je Generation abweichen.
    /// </summary>
    public const string WflNamespaceSuffix = "WFL";

    /// <summary>
    /// Suffix der TO-Typnamen (<c>{View}TO</c>). Invariant, weil die TO-Typen in den
    /// Interface-Signaturen (<c>{Trigger}(XyzTO to)</c>) sichtbar sind.
    /// </summary>
    public const string ToClassNameSuffix = "TO";

    // -- Annotation-Tags (C#→Nav-Rückweg) ------------------------------------------------------------------

    /// <summary>Gemeinsames Präfix aller Nav-Annotation-Tags (<c>Nav</c>).</summary>
    public const string AnnotationTagPrefix = "Nav";

    /// <summary>Tag, das die generierte Datei mit ihrer <c>.nav</c>-Quelle verknüpft (<c>NavFile</c>).</summary>
    public const string AnnotationTagNavFile = "NavFile";

    /// <summary>Tag am generierten Task-Typ (<c>NavTask</c>).</summary>
    public const string AnnotationTagNavTask = "NavTask";

    /// <summary>Tag an einer generierten Trigger-Methode (<c>NavTrigger</c>).</summary>
    public const string AnnotationTagNavTrigger = "NavTrigger";

    /// <summary>Tag an der generierten Init-Methode (<c>NavInit</c>).</summary>
    public const string AnnotationTagNavInit = "NavInit";

    /// <summary>Tag an einer generierten Exit-Methode (<c>NavExit</c>).</summary>
    public const string AnnotationTagNavExit = "NavExit";

    /// <summary>Tag an einer generierten <c>{Choice}Logic</c>-Methode (<c>NavChoice</c>).</summary>
    public const string AnnotationTagNavChoice = "NavChoice";

    /// <summary>Tag an einem generierten Init-Aufruf (<c>NavInitCall</c>).</summary>
    public const string AnnotationTagNavInitCall = "NavInitCall";

}
