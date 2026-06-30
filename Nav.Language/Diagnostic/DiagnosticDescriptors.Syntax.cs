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
        /// Preprocessor directives must appear as the first non-whitespace character on a line
        /// </summary>
        public static readonly DiagnosticDescriptor Nav3001PreprocessorDirectiveMustAppearOnFirstNonWhitespacePosition = new(
            id             : DiagnosticId.Nav3001,
            messageFormat  : "Preprocessor directives must appear as the first non-whitespace character on a line",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

    }
}