namespace Pharmatechnik.Nav.Language;

public static partial class DiagnosticDescriptors {
       
    /// <summary>
    /// Die Syntax-Diagnosen des Katalogs — Meldungen von Lexer und Parser (<c>Syntax\</c>),
    /// einschließlich der Präprozessor-/<c>#version</c>-Direktiven. Alle tragen die Kategorie
    /// <see cref="DiagnosticCategory.Syntax"/>.
    /// </summary>
    public static class Syntax {

        /// <summary>Die gemeinsame Kategorie aller hier definierten Deskriptoren (<see cref="DiagnosticCategory.Syntax"/>).</summary>
        public const DiagnosticCategory Category = DiagnosticCategory.Syntax;
        /// <summary>Der voreingestellte Schweregrad der einfachen Syntaxfehler (<see cref="DiagnosticSeverity.Error"/>).</summary>
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Error;
           
        /// <summary>
        /// Unexpected character '{0}'
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0000UnexpectedCharacter = new(
            id             : DiagnosticId.Nav0000,
            messageFormat  : "Unexpected character '{0}'",
            category       : Category,
            defaultSeverity: Severity
        );

        //------------------------------
        // Preprocessor Errors

        /// <summary>
        /// Invalid preprocessor directive
        /// </summary>
        public static readonly DiagnosticDescriptor Nav3000InvalidPreprocessorDirective = new(
            id             : DiagnosticId.Nav3000,
            messageFormat  : "Invalid preprocessor directive",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// Unknown pragma '{0}'
        /// </summary>
        public static readonly DiagnosticDescriptor Nav3001UnknownPragma = new(
            id             : DiagnosticId.Nav3001,
            messageFormat  : "Unknown pragma '{0}'",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// Invalid '#version' directive; expected a non-negative integer version number
        /// </summary>
        public static readonly DiagnosticDescriptor Nav3002InvalidVersionDirective = new(
            id             : DiagnosticId.Nav3002,
            messageFormat  : "Invalid '#version' directive; expected a non-negative integer version number",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// '#version' must appear at the top of the file
        /// </summary>
        public static readonly DiagnosticDescriptor Nav3003VersionDirectiveMustAppearAtTopOfFile = new(
            id             : DiagnosticId.Nav3003,
            messageFormat  : "'#version' must appear at the top of the file, preceded only by comments or whitespace",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// Duplicate '#version' directive
        /// </summary>
        public static readonly DiagnosticDescriptor Nav3004DuplicateVersionDirective = new(
            id             : DiagnosticId.Nav3004,
            messageFormat  : "Duplicate '#version' directive; only the first one is used",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

    }
}