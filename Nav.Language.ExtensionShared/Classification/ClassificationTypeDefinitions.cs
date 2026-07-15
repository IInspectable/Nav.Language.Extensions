#region Using Directives

using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

using Pharmatechnik.Nav.Language.Text;

using CSharpClassificationTypeNames=Microsoft.CodeAnalysis.Classification.ClassificationTypeNames;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Classification; 

// ReSharper disable UnassignedField.Local
#pragma warning disable 0169
/// <summary>
/// MEF-Registrierung sämtlicher Nav-eigener Klassifizierungstypen samt ihrer Darstellung. Für jeden
/// Namen aus <see cref="ClassificationTypeNames"/> wird hier ein
/// <see cref="Microsoft.VisualStudio.Text.Classification.ClassificationTypeDefinition"/> exportiert
/// (mit Basis-Klassifizierung via <c>[BaseDefinition]</c>) und eine
/// <see cref="Microsoft.VisualStudio.Text.Classification.ClassificationFormatDefinition"/> für Farbe,
/// Schriftschnitt und Priorität. Zusätzlich liefert
/// <see cref="GetSyntaxTokenClassificationMap"/> die Zuordnung der lexikalischen
/// <see cref="TextClassification"/>-Kategorien auf die registrierten Klassifizierungstypen, die die
/// Tagger zum Einfärben nutzen.
/// </summary>
static class ClassificationTypeDefinitions {

    //======================================
    //      Die Farben sollen derzeit nicht 
    //      anpassbar sein.
    //======================================
    /// <summary>
    /// Schaltet die Sichtbarkeit der Nav-Formatdefinitionen im Optionsdialog „Schriftarten und Farben".
    /// Bewusst <c>false</c>, da die Nav-Farben derzeit nicht anpassbar sein sollen.
    /// </summary>
    static class Is {

        /// <summary>Steuert das <c>[UserVisible]</c>-Attribut der Formatdefinitionen (aktuell <c>false</c>).</summary>
        public const bool UserVisible = false;

    }

    #region Keyword

    /// <summary>Klassifizierungstyp für Nav-Schlüsselwörter (<see cref="ClassificationTypeNames.Keyword"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.Keyword)] [BaseDefinition(PredefinedClassificationTypeNames.Keyword)]
    public static ClassificationTypeDefinition Keyword;

    /// <summary>Darstellung (Anzeigename) der Schlüsselwort-Klassifizierung.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.Keyword)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Default)]
    public sealed class KeywordClassificationFormatDefinition: ClassificationFormatDefinition {

        public KeywordClassificationFormatDefinition() {
            DisplayName = "Nav Keyword";
        }

    }

    #endregion

    #region ControlKeyword

    /// <summary>Klassifizierungstyp für Steuerfluss-Schlüsselwörter (<see cref="ClassificationTypeNames.ControlKeyword"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] 
    [Name(ClassificationTypeNames.ControlKeyword)] 
    [BaseDefinition(CSharpClassificationTypeNames.ControlKeyword)]
    public static ClassificationTypeDefinition ControlKeyword;

    /// <summary>Darstellung (Anzeigename) der Steuerfluss-Schlüsselwort-Klassifizierung.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.ControlKeyword)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Default)]
    public sealed class ControlKeywordClassificationFormatDefinition: ClassificationFormatDefinition {

        public ControlKeywordClassificationFormatDefinition() {
            DisplayName = "Nav Control Keyword";
        }

    }

    #endregion

    #region Comment

    /// <summary>Klassifizierungstyp für Kommentare (<see cref="ClassificationTypeNames.Comment"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.Comment)] [BaseDefinition(PredefinedClassificationTypeNames.Comment)]
    public static ClassificationTypeDefinition Comment;

    /// <summary>Darstellung (Anzeigename) der Kommentar-Klassifizierung.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.Comment)]
    [UserVisible(Is.UserVisible)] // This should be visible to the end user
    [Order(Before = Priority.Default)]
    // Set the priority to be after the default classifiers
    public sealed class CommentClassificationFormatDefinition: ClassificationFormatDefinition {

        public CommentClassificationFormatDefinition() {
            DisplayName = "Nav Comment"; // Human readable version of the name
        }

    }

    #endregion

    #region Identifier

    /// <summary>Klassifizierungstyp für allgemeine Bezeichner (<see cref="ClassificationTypeNames.Identifier"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.Identifier)] [BaseDefinition(PredefinedClassificationTypeNames.Identifier)]
    public static ClassificationTypeDefinition Identifier;

    /// <summary>Darstellung (Anzeigename) der Bezeichner-Klassifizierung.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.Identifier)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Default)]
    public sealed class IdentifierClassificationFormatDefinition: ClassificationFormatDefinition {

        public IdentifierClassificationFormatDefinition() {
            DisplayName = "Nav Identifier";
        }

    }

    #endregion

    #region String

    /// <summary>Klassifizierungstyp für String-Literale (<see cref="ClassificationTypeNames.StringLiteral"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.StringLiteral)] [BaseDefinition(PredefinedClassificationTypeNames.String)]
    public static ClassificationTypeDefinition String;

    /// <summary>Darstellung (Anzeigename) der String-Klassifizierung.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.StringLiteral)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Default)]
    public sealed class StringClassificationFormatDefinition: ClassificationFormatDefinition {

        public StringClassificationFormatDefinition() {
            DisplayName = "Nav String";
        }

    }

    #endregion

    #region FormName

    /// <summary>Klassifizierungstyp für GUI-Knoten (View-/Dialog-Knoten, <see cref="ClassificationTypeNames.GuiNode"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.GuiNode)] [BaseDefinition(CSharpClassificationTypeNames.LocalName)]
    public static ClassificationTypeDefinition Type;

    /// <summary>Darstellung (Anzeigename) der GUI-Knoten-Klassifizierung.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.GuiNode)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Default)]
    public sealed class FormNameClassificationFormatDefinition: ClassificationFormatDefinition {

        public FormNameClassificationFormatDefinition() {
            DisplayName = "Nav Form Name";
        }

    }

    #endregion

    #region TaskName

    /// <summary>Klassifizierungstyp für Task-Namen (<see cref="ClassificationTypeNames.TaskName"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.TaskName)] [BaseDefinition(CSharpClassificationTypeNames.ClassName)]
    public static ClassificationTypeDefinition TaskName;

    /// <summary>Darstellung (Anzeigename) der Task-Namen-Klassifizierung.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.TaskName)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Default)]
    public sealed class TaskNameClassificationFormatDefinition: ClassificationFormatDefinition {

        public TaskNameClassificationFormatDefinition() {
            DisplayName = "Nav Task Name";
        }

    }

    #endregion

    #region TypeName

    /// <summary>Klassifizierungstyp für Typnamen (<see cref="ClassificationTypeNames.TypeName"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.TypeName)] [BaseDefinition(CSharpClassificationTypeNames.ClassName)]
    public static ClassificationTypeDefinition TypeName;

    /// <summary>Darstellung (Anzeigename) der Typnamen-Klassifizierung.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.TypeName)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Default)]
    public sealed class TypeNameClassificationFormatDefinition: ClassificationFormatDefinition {

        public TypeNameClassificationFormatDefinition() {
            DisplayName = "Nav Type Name";
        }

    }

    #endregion

    #region Punctuation

    /// <summary>Klassifizierungstyp für Satzzeichen (<see cref="ClassificationTypeNames.Punctuation"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.Punctuation)] [BaseDefinition(CSharpClassificationTypeNames.Punctuation)]
    public static ClassificationTypeDefinition Punctuation;

    /// <summary>Darstellung (Anzeigename) der Satzzeichen-Klassifizierung.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.Punctuation)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Default)]
    public sealed class PunctuationClassificationFormatDefinition: ClassificationFormatDefinition {

        public PunctuationClassificationFormatDefinition() {
            DisplayName = "Nav Punctuation";
        }

    }

    #endregion

    #region Unknown

    /// <summary>
    /// Klassifizierungstyp für unbekannte/fehlerhafte Token (<see cref="ClassificationTypeNames.Unknown"/>);
    /// erbt von der VS-Basis-Klassifizierung „Syntax Error".
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.Unknown)] [BaseDefinition("Syntax Error")]
    public static ClassificationTypeDefinition Unknown;

    /// <summary>Darstellung (Anzeigename) der Unbekannt-Klassifizierung.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.Unknown)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Default)]
    public sealed class UnknownClassificationFormatDefinition: ClassificationFormatDefinition {

        public UnknownClassificationFormatDefinition() {
            DisplayName = "Nav Unknown";
        }

    }

    #endregion

    #region DeadCode

    /// <summary>Klassifizierungstyp für toten Code (<see cref="ClassificationTypeNames.DeadCode"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.DeadCode)] [BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
    public static ClassificationTypeDefinition DeadCode;

    /// <summary>Darstellung des toten Codes: halbtransparent (<c>ForegroundOpacity = 0.5</c>).</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.DeadCode)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.High)]
    public sealed class DeadCodeClassificationFormatDefinition: ClassificationFormatDefinition {

        public DeadCodeClassificationFormatDefinition() {
            DisplayName       = "Nav Dead Code";
            ForegroundOpacity = 0.5;
        }

    }

    #endregion

    #region ChoiceNode

    /// <summary>Klassifizierungstyp für Choice-Knoten (<see cref="ClassificationTypeNames.ChoiceNode"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.ChoiceNode)] [BaseDefinition(CSharpClassificationTypeNames.MethodName)]
    public static ClassificationTypeDefinition ChoiceNode;

    /// <summary>Darstellung der Choice-Knoten: kursiv.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.ChoiceNode)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Low)]
    public sealed class ChoiceNodeClassificationFormatDefinition: ClassificationFormatDefinition {

        public ChoiceNodeClassificationFormatDefinition() {
            IsItalic = true;
        }

    }

    #endregion

    #region ConnectionPoint

    /// <summary>Klassifizierungstyp für ConnectionPoints (Init-/Exit-Knoten, <see cref="ClassificationTypeNames.ConnectionPoint"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.ConnectionPoint)] [BaseDefinition(ClassificationTypeNames.Identifier)]
    public static ClassificationTypeDefinition ConnectionPoint;

    /// <summary>Darstellung der ConnectionPoints: fett.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.ConnectionPoint)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Low)]
    public sealed class ConnectionPointClassificationFormatDefinition: ClassificationFormatDefinition {

        public ConnectionPointClassificationFormatDefinition() {
            // IsItalic = true;
            IsBold = true;
        }

    }

    #endregion

    #region Underline

    /// <summary>Klassifizierungstyp für Unterstreichungen (<see cref="ClassificationTypeNames.Underline"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.Underline)] [BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)]
    public static ClassificationTypeDefinition Underline;

    /// <summary>Darstellung der Unterstreichung: fügt eine an die Schriftgröße angepasste Unterstreichungs-Dekoration hinzu.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.Underline)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Default)]
    public sealed class UnderlineClassificationFormatDefinition: ClassificationFormatDefinition {

        public UnderlineClassificationFormatDefinition() {
            DisplayName = "Nav Underline";

            var underline = new System.Windows.TextDecoration {
                PenThicknessUnit = System.Windows.TextDecorationUnit.FontRecommended
            };
            
            TextDecorations ??= new System.Windows.TextDecorationCollection();

            TextDecorations.Add(underline);
        }

    }

    #endregion

    #region ParameterName

    /// <summary>Klassifizierungstyp für Parameternamen (<see cref="ClassificationTypeNames.ParameterName"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] 
    [Name(ClassificationTypeNames.ParameterName)] 
    [BaseDefinition(CSharpClassificationTypeNames.ParameterName)]
    public static ClassificationTypeDefinition Parameter;

    /// <summary>Darstellung (Anzeigename) der Parameternamen-Klassifizierung.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.ParameterName)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Default)]
    public sealed class ParameterNameClassificationFormatDefinition: ClassificationFormatDefinition {

        public ParameterNameClassificationFormatDefinition() {
            DisplayName = "Nav ParameterName";              
        }

    }

    #endregion

    #region PreprocessorKeyword

    /// <summary>Klassifizierungstyp für Präprozessor-Schlüsselwörter (<see cref="ClassificationTypeNames.PreprocessorKeyword"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.PreprocessorKeyword)] [BaseDefinition(CSharpClassificationTypeNames.PreprocessorKeyword)]
    public static ClassificationTypeDefinition PreprocessorKeyword;

    /// <summary>Darstellung (Anzeigename) der Präprozessor-Schlüsselwort-Klassifizierung.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.PreprocessorKeyword)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Default)]
    public sealed class PreprocessorKeywordClassificationFormatDefinition: ClassificationFormatDefinition {

        public PreprocessorKeywordClassificationFormatDefinition() {
            DisplayName = "Nav Preprocessor Keyword";
        }

    }

    #endregion

    #region PreprocessorText

    /// <summary>Klassifizierungstyp für den Text von Präprozessor-Direktiven (<see cref="ClassificationTypeNames.PreprocessorText"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.PreprocessorText)] [BaseDefinition(CSharpClassificationTypeNames.PreprocessorText)]
    public static ClassificationTypeDefinition PreprocessorText;

    /// <summary>Darstellung (Anzeigename) der Präprozessor-Text-Klassifizierung.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.PreprocessorText)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Default)]
    public sealed class PreprocessorTextClassificationFormatDefinition: ClassificationFormatDefinition {

        public PreprocessorTextClassificationFormatDefinition() {
            DisplayName = "Nav Preprocessor Text";
        }

    }

    #endregion

    #region NumberLiteral

    /// <summary>Klassifizierungstyp für Zahlenliterale (<see cref="ClassificationTypeNames.NumberLiteral"/>).</summary>
    [Export(typeof(ClassificationTypeDefinition))] [Name(ClassificationTypeNames.NumberLiteral)] [BaseDefinition(CSharpClassificationTypeNames.NumericLiteral)]
    public static ClassificationTypeDefinition NumberLiteral;

    /// <summary>Darstellung (Anzeigename) der Zahlenliteral-Klassifizierung.</summary>
    [Export(typeof(EditorFormatDefinition))]
    [Name(ClassificationTypeNames.NumberLiteral)]
    [UserVisible(Is.UserVisible)]
    [Order(Before = Priority.Default)]
    public sealed class NumberLiteralClassificationFormatDefinition: ClassificationFormatDefinition {

        public NumberLiteralClassificationFormatDefinition() {
            DisplayName = "Nav Number";
        }

    }

    #endregion

    /// <summary>
    /// Bildet die lexikalischen <see cref="TextClassification"/>-Kategorien des Nav-Lexers auf die in VS
    /// registrierten <see cref="IClassificationType"/>-Instanzen ab. Der syntaktische Tagger
    /// (<see cref="SyntacticClassificationTagger"/>) nutzt diese Zuordnung, um jedem Token seinen
    /// Klassifizierungstyp und damit seine Einfärbung zuzuweisen.
    /// </summary>
    /// <param name="registry">Registrierungsdienst, über den die benannten Klassifizierungstypen aufgelöst werden.</param>
    /// <returns>Unveränderliche Zuordnung von <see cref="TextClassification"/> auf <see cref="IClassificationType"/>.</returns>
    public static ImmutableDictionary<TextClassification, IClassificationType> GetSyntaxTokenClassificationMap(IClassificationTypeRegistryService registry) {

        var classificationMap = new Dictionary<TextClassification, IClassificationType> {
            {TextClassification.Skiped             , registry.GetClassificationType(ClassificationTypeNames.Unknown)},
            {TextClassification.Unknown            , registry.GetClassificationType(ClassificationTypeNames.Unknown)},
            {TextClassification.Comment            , registry.GetClassificationType(ClassificationTypeNames.Comment)},
            {TextClassification.Keyword            , registry.GetClassificationType(ClassificationTypeNames.Keyword)},
            {TextClassification.ControlKeyword     , registry.GetClassificationType(ClassificationTypeNames.ControlKeyword)},
            {TextClassification.Identifier         , registry.GetClassificationType(ClassificationTypeNames.Identifier)},
            {TextClassification.Punctuation        , registry.GetClassificationType(ClassificationTypeNames.Punctuation)},
            {TextClassification.StringLiteral      , registry.GetClassificationType(ClassificationTypeNames.StringLiteral)},
            {TextClassification.TypeName           , registry.GetClassificationType(ClassificationTypeNames.TypeName)},
            {TextClassification.TaskName           , registry.GetClassificationType(ClassificationTypeNames.TaskName)},
            {TextClassification.ConnectionPoint    , registry.GetClassificationType(ClassificationTypeNames.ConnectionPoint)},
            {TextClassification.ChoiceNode         , registry.GetClassificationType(ClassificationTypeNames.ChoiceNode)},
            {TextClassification.GuiNode            , registry.GetClassificationType(ClassificationTypeNames.GuiNode)},
            {TextClassification.DeadCode           , registry.GetClassificationType(ClassificationTypeNames.DeadCode)},
            {TextClassification.ParameterName      , registry.GetClassificationType(ClassificationTypeNames.ParameterName)},
            {TextClassification.PreprocessorKeyword, registry.GetClassificationType(ClassificationTypeNames.PreprocessorKeyword)},
            {TextClassification.PreprocessorText   , registry.GetClassificationType(ClassificationTypeNames.PreprocessorText)},
            {TextClassification.NumberLiteral      , registry.GetClassificationType(ClassificationTypeNames.NumberLiteral)},
        };

        return classificationMap.ToImmutableDictionary();
    }

}