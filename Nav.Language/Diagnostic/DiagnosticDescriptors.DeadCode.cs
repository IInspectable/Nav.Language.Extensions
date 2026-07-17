namespace Pharmatechnik.Nav.Language;

public static partial class DiagnosticDescriptors {

    // Unbenutzer Code wird etwas gesondert behandelt, da weder Error noch Warning.
    // Im Editor wird der Code z.B. etwas abgeduckellt dargestellt. Deshalb bekommt
    // diese Art von Diagnostic eine eigene Kategorie.
    /// <summary>
    /// Die Tote-Code-Diagnosen des Katalogs (<c>Nav1xxx</c>) — Hinweise auf Deklarationen oder
    /// Direktiven, die der generierte Code nicht benötigt und die gefahrlos entfernt werden können.
    /// Sie bilden eine eigene Kategorie (<see cref="DiagnosticCategory.DeadCode"/>), weil sie weder
    /// Fehler noch Warnung im engeren Sinn sind und im Editor eigens (abgedunkelt) dargestellt werden.
    /// </summary>
    public static class DeadCode {

        /// <summary>Die gemeinsame Kategorie aller hier definierten Deskriptoren (<see cref="DiagnosticCategory.DeadCode"/>).</summary>
        public const DiagnosticCategory Category = DiagnosticCategory.DeadCode;
        /// <summary>Der gemeinsame Schweregrad der Tote-Code-Hinweise (<see cref="DiagnosticSeverity.Warning"/>).</summary>
        public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;

        /// <summary>
        /// The include directive for '{0}' appeared previously in this file and can be safely removed
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1001IncludeDirectiveForFile0AppearedPreviously = new(
            id             : DiagnosticId.Nav1001,
            messageFormat  : "The include directive for '{0}' appeared previously in this file and can be safely removed",
            category       : Category,
            defaultSeverity: Severity
        );

        /// <summary>
        /// The using directive for '{0}' appeared previously in this file and can be safely removed
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1002UsingDirective0AppearedPreviously = new(
            id             : DiagnosticId.Nav1002,
            messageFormat  : "The using directive for '{0}' appeared previously in this file and can be safely removed",
            category       : Category,
            defaultSeverity: Severity
        );

        /// <summary>
        /// Taskref directive is not required by the code and can be safely removed
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1003IncludeNotRequired = new(
            id             : DiagnosticId.Nav1003,
            messageFormat  : "Taskref directive is not required by the code and can be safely removed",
            category       : Category,
            defaultSeverity: Severity
        );

        /// <summary>
        /// Taskref '{0}' is not required by the code and can be safely removed
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1005TaskDeclaration0NotRequired = new(
            id             : DiagnosticId.Nav1005,
            messageFormat  : "Taskref '{0}' is not required by the code and can be safely removed",
            category       : Category,
            defaultSeverity: Severity
        );

        /// <summary>
        /// The self-referencing taskref directive is not required by the code and can be safely removed
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1006SelfReferencingIncludeNotRequired = new(
            id: DiagnosticId.Nav1006,
            messageFormat  : "The self-referencing taskref directive is not required by the code and can be safely removed",
            category       : Category,
            defaultSeverity: Severity
        );


        /// <summary>
        /// The choice node '{0}' has no incoming edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1007ChoiceNode0HasNoIncomingEdges = new(
            id             : DiagnosticId.Nav1007,
            messageFormat  : "The choice node '{0}' has no incoming edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The choice node '{0}' has no outgoing edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1008ChoiceNode0HasNoOutgoingEdges = new(
            id             : DiagnosticId.Nav1008,
            messageFormat  : "The choice node '{0}' has no outgoing edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The choice node '{0}' is not required by the code and can be safely removed
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1009ChoiceNode0NotRequired = new(
            id             : DiagnosticId.Nav1009,
            messageFormat  : "The choice node '{0}' is not required by the code and can be safely removed",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

            
        /// <summary>
        /// The task node '{0}' has no incoming edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1010TaskNode0HasNoIncomingEdges = new(
            id             : DiagnosticId.Nav1010,
            messageFormat  : "The task node '{0}' has no incoming edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );
        
        /// <summary>
        /// The task node '{0}' is not required by the code and can be safely removed
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1012TaskNode0NotRequired = new(
            id             : DiagnosticId.Nav1012,
            messageFormat  : "The task node '{0}' is not required by the code and can be safely removed",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The dialog node '{0}' is not required by the code and can be safely removed
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1014DialogNode0NotRequired = new(
            id             : DiagnosticId.Nav1014,
            messageFormat  : "The dialog node '{0}' is not required by the code and can be safely removed",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The dialog node '{0}' has no incoming edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1015DialogNode0HasNoIncomingEdges = new(
            id             : DiagnosticId.Nav1015,
            messageFormat  : "The dialog node '{0}' has no incoming edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The dialog node '{0}' has no outgoing edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1016DialogNode0HasNoOutgoingEdges = new(
            id             : DiagnosticId.Nav1016,
            messageFormat  : "The dialog node '{0}' has no outgoing edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The view node '{0}' is not required by the code and can be safely removed
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1017ViewNode0NotRequired = new(
            id             : DiagnosticId.Nav1017,
            messageFormat  : "The view node '{0}' is not required by the code and can be safely removed",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The view node '{0}' has no incoming edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1018ViewNode0HasNoIncomingEdges = new(
            id             : DiagnosticId.Nav1018,
            messageFormat  : "The view node '{0}' has no incoming edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The view node '{0}' has no outgoing edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav1019ViewNode0HasNoOutgoingEdges = new(
            id             : DiagnosticId.Nav1019,
            messageFormat  : "The view node '{0}' has no outgoing edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );
    }
}