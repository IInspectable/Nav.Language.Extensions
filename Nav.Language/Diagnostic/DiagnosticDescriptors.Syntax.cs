namespace Pharmatechnik.Nav.Language; 

public static partial class DiagnosticDescriptors {
       
    public static class Syntax {

        public const DiagnosticCategory Category = DiagnosticCategory.Syntax;
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
        /// Invalid '#pragma version' directive; expected a non-negative integer version number
        /// </summary>
        public static readonly DiagnosticDescriptor Nav3002InvalidPragmaVersion = new(
            id             : DiagnosticId.Nav3002,
            messageFormat  : "Invalid '#pragma version' directive; expected a non-negative integer version number",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// '#pragma version' must appear at the top of the file
        /// </summary>
        public static readonly DiagnosticDescriptor Nav3003PragmaVersionMustAppearAtTopOfFile = new(
            id             : DiagnosticId.Nav3003,
            messageFormat  : "'#pragma version' must appear at the top of the file, preceded only by comments or whitespace",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// Duplicate '#pragma version' directive
        /// </summary>
        public static readonly DiagnosticDescriptor Nav3004DuplicatePragmaVersion = new(
            id             : DiagnosticId.Nav3004,
            messageFormat  : "Duplicate '#pragma version' directive; only the first one is used",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

    }
}