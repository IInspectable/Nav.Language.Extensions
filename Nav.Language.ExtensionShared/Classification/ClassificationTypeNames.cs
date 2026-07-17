namespace Pharmatechnik.Nav.Language.Extension.Classification; 

/// <summary>
/// Stabile Namen der Nav-eigenen Klassifizierungstypen (VS-Editor). Jeder Name identifiziert einen
/// über MEF exportierten <see cref="Microsoft.VisualStudio.Text.Classification.IClassificationType"/>
/// und wird sowohl in <see cref="ClassificationTypeDefinitions"/> (Definition von Typ und
/// Darstellung) als auch in den Taggern zum Nachschlagen im
/// <see cref="Microsoft.VisualStudio.Text.Classification.IClassificationTypeRegistryService"/> genutzt.
/// Die Konstantenwerte (z.B. <c>"NavKeyword"</c>) sind der in VS registrierte Bezeichner und dürfen
/// sich nicht ändern.
/// </summary>
static class ClassificationTypeNames {

    /// <summary>Nav-Schlüsselwörter (z.B. <c>task</c>, <c>init</c>).</summary>
    public const string Keyword             = "NavKeyword";
    /// <summary>Steuerfluss-Schlüsselwörter (z.B. <c>goto</c>, <c>if</c>) — von gewöhnlichen Schlüsselwörtern abgesetzt.</summary>
    public const string ControlKeyword      = "NavControlKeyword";
    /// <summary>Kanten/Transitionen (aktuell nicht in der Klassifizierungs-Zuordnung verdrahtet).</summary>
    public const string Edge                = "NavEdge";
    /// <summary>Kommentare.</summary>
    public const string Comment             = "NavComment";
    /// <summary>Allgemeine Bezeichner.</summary>
    public const string Identifier          = "NavIdentifier";
    /// <summary>String-Literale.</summary>
    public const string StringLiteral       = "NavString";
    /// <summary>Satzzeichen/Interpunktion.</summary>
    public const string Punctuation         = "NavPunctuation";
    /// <summary>Unbekannte bzw. fehlerhafte (übersprungene) Token — als Syntaxfehler eingefärbt.</summary>
    public const string Unknown             = "NavUnknown";
    /// <summary>GUI-Knoten (View-/Dialog-Knoten) des Workflows.</summary>
    public const string GuiNode             = "NavFormName";
    /// <summary>Task-Namen.</summary>
    public const string TaskName            = "NavTaskName";
    /// <summary>Typnamen (Klassen).</summary>
    public const string TypeName            = "NavClassName";
    /// <summary>Toter Code (per Diagnose <see cref="DiagnosticCategory.DeadCode"/> markiert), abgeschwächt dargestellt.</summary>
    public const string DeadCode            = "NavDeadCode";
    /// <summary>Unterstreichung (Grundlage des Underline-Adornments, siehe <see cref="UnderlineClassifier"/>).</summary>
    public const string Underline           = "NavUnderline";
    /// <summary>Choice-Knoten.</summary>
    public const string ChoiceNode          = "NavChoiceNode";
    /// <summary>ConnectionPoints (Init-/Exit-Knoten des Workflows).</summary>
    public const string ConnectionPoint     = "NavConnectionPoint";
    /// <summary>Parameternamen.</summary>
    public const string ParameterName       = "NavParameterName";
    /// <summary>Text von Präprozessor-Direktiven.</summary>
    public const string PreprocessorText    = "NavPreprocessorText";
    /// <summary>Schlüsselwörter von Präprozessor-Direktiven.</summary>
    public const string PreprocessorKeyword = "NavPreprocessorKeyword";
    /// <summary>Zahlenliterale.</summary>
    public const string NumberLiteral       = "NavNumber";

}