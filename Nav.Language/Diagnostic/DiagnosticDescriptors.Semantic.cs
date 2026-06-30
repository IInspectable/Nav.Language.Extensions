namespace Pharmatechnik.Nav.Language; 

public partial class DiagnosticDescriptors {

    public static class Semantic {

        public const DiagnosticCategory Category = DiagnosticCategory.Semantic;

        /// <summary>
        /// Source file needs to be saved before include directives can be processed
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0003SourceFileNeedsToBeSavedBeforeIncludeDirectiveCanBeProcessed = new(
            id             : DiagnosticId.Nav0003,
            messageFormat  : "Source file needs to be saved before include directives can be processed",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// File '{0}' not found
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0004File0NotFound = new(
            id             : DiagnosticId.Nav0004,
            messageFormat  : "File '{0}' not found",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// The file '{0}' has some errors
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0005IncludeFile0HasSomeErrors = new(
            id             : DiagnosticId.Nav0005,
            messageFormat  : "The file '{0}' has some errors",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// Cannot resolve task '{0}'
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0010CannotResolveTask0 = new(
            id             : DiagnosticId.Nav0010,
            messageFormat  : "Cannot resolve task '{0}'",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// Cannot resolve node '{0}'
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0011CannotResolveNode0 = new(
            id             : DiagnosticId.Nav0011,
            messageFormat  : "Cannot resolve node '{0}'",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// Cannot resolve exit '{0}'
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0012CannotResolveExit0 = new(
            id             : DiagnosticId.Nav0012,
            messageFormat  : "Cannot resolve exit '{0}'",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// A task with the name '{0}' is already declared
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0020TaskWithName0AlreadyDeclared = new(
            id             : DiagnosticId.Nav0020,
            messageFormat  : "A task with the name '{0}' is already declared",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// A connection point with the name '{0}' is already declared
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0021ConnectionPointWithName0AlreadyDeclared = new(
            id             : DiagnosticId.Nav0021,
            messageFormat  : "A connection point with the name '{0}' is already declared",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// A node with the name '{0}' is already declared
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0022NodeWithName0AlreadyDeclared = new(
            id             : DiagnosticId.Nav0022,
            messageFormat  : "A node with the name '{0}' is already declared",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// An outgoing edge for trigger '{0}' is already declared
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0023AnOutgoingEdgeForTrigger0IsAlreadyDeclared = new(
            id             : DiagnosticId.Nav0023,
            messageFormat  : "An outgoing edge for trigger '{0}' is already declared",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// An outgoing edge for exit '{0}' is already declared
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0024OutgoingEdgeForExit0AlreadyDeclared = new(
            id             : DiagnosticId.Nav0024,
            messageFormat  : "An outgoing edge for exit '{0}' is already declared",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// No outgoing edge declared for exit '{0}'
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0025NoOutgoingEdgeForExit0Declared = new(
            id             : DiagnosticId.Nav0025,
            messageFormat  : "No outgoing edge declared for exit '{0}'",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// Trigger '{0}' is already declared
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0026TriggerWithName0AlreadyDeclared = new(
            id             : DiagnosticId.Nav0026,
            messageFormat  : "Trigger '{0}' is already declared",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// The task '{0}' must not contain outgoing edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0100TaskNode0MustNotContainLeavingEdges = new(
            id             : DiagnosticId.Nav0100,
            messageFormat  : "The task '{0}' must not contain outgoing edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// An exit node must not contain outgoing edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0101ExitNodeMustNotContainLeavingEdges = new(
            id             : DiagnosticId.Nav0101,
            messageFormat  : "An exit node must not contain outgoing edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// An end node must not contain outgoing edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0102EndNodeMustNotContainLeavingEdges = new(
            id             : DiagnosticId.Nav0102,
            messageFormat  : "An end node must not contain outgoing edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// An init node must not contain incoming edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0103InitNodeMustNotContainIncomingEdges = new(
            id             : DiagnosticId.Nav0103,
            messageFormat  : "An init node must not contain incoming edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// Choice node '{0}' can only be reached by a goto edge (-->)
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0104ChoiceNode0MustOnlyReachedByGoTo = new(
            id             : DiagnosticId.Nav0104,
            messageFormat  : "Choice node '{0}' can only be reached by a goto edge (-->)",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// Exit node '{0}' can only be reached by a goto edge (-->)
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0105ExitNode0MustOnlyReachedByGoTo = new(
            id             : DiagnosticId.Nav0105,
            messageFormat  : "Exit node '{0}' can only be reached by a goto edge (-->)",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// End node '{0}' can only be reached by a goto edge (-->)
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0106EndNode0MustOnlyReachedByGoTo = new(
            id             : DiagnosticId.Nav0106,
            messageFormat  : "End node '{0}' can only be reached by a goto edge (-->)",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// The exit node '{0}' has no incoming edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0107ExitNode0HasNoIncomingEdges = new(
            id             : DiagnosticId.Nav0107,
            messageFormat  : "The exit node '{0}' has no incoming edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The end node has no incoming edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0108EndNodeHasNoIncomingEdges = new(
            id             : DiagnosticId.Nav0108,
            messageFormat  : "The end node has no incoming edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The init node '{0}' has no outgoing edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0109InitNode0HasNoOutgoingEdges = new(
            id             : DiagnosticId.Nav0109,
            messageFormat  : "The init node '{0}' has no outgoing edges",
            category       : Category,
            // TODO Error oder Warning - noch klären
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// '{0}' edge not allowed here because '{1}' is reachable from init node '{2}'
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0110Edge0NotAllowedIn1BecauseItsReachableFromInit2 = new(
            id             : DiagnosticId.Nav0110,
            messageFormat  : "'{0}' edge not allowed here because '{1}' is reachable from init node '{2}'",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// The choice node '{0}' has no incoming edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0111ChoiceNode0HasNoIncomingEdges = new(
            id             : DiagnosticId.Nav0111,
            messageFormat  : "The choice node '{0}' has no incoming edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The choice node '{0}' has no outgoing edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0112ChoiceNode0HasNoOutgoingEdges = new(
            id             : DiagnosticId.Nav0112,
            messageFormat  : "The choice node '{0}' has no outgoing edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The task node '{0}' has no incoming edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0113TaskNode0HasNoIncomingEdges = new(
            id             : DiagnosticId.Nav0113,
            messageFormat  : "The task node '{0}' has no incoming edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The dialog node '{0}' has no incoming edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0114DialogNode0HasNoIncomingEdges = new(
            id             : DiagnosticId.Nav0114,
            messageFormat  : "The dialog node '{0}' has no incoming edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The dialog node '{0}' has no outgoing edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0115DialogNode0HasNoOutgoingEdges = new(
            id             : DiagnosticId.Nav0115,
            messageFormat  : "The dialog node '{0}' has no outgoing edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The view node '{0}' has no incoming edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0116ViewNode0HasNoIncomingEdges = new(
            id             : DiagnosticId.Nav0116,
            messageFormat  : "The view node '{0}' has no incoming edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// The view node '{0}' has no outgoing edges
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0117ViewNode0HasNoOutgoingEdges = new(
            id             : DiagnosticId.Nav0117,
            messageFormat  : "The view node '{0}' has no outgoing edges",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Warning
        );

        /// <summary>
        /// Signal trigger not allowed after init
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0200SignalTriggerNotAllowedAfterInit = new(
            id             : DiagnosticId.Nav0200,
            messageFormat  : "Signal trigger not allowed after init",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// Spontaneous not allowed in signal trigger
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0201SpontaneousNotAllowedInSignalTrigger = new(
            id             : DiagnosticId.Nav0201,
            messageFormat  : "Spontaneous not allowed in signal trigger",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// Trigger not allowed after choice
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0203TriggerNotAllowedAfterChoice = new(
            id             : DiagnosticId.Nav0203,
            messageFormat  : "Trigger not allowed after choice",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// Conditions are not allowed in trigger transitions
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0220ConditionsAreNotAllowedInTriggerTransitions = new(
            id             : DiagnosticId.Nav0220,
            messageFormat  : "Conditions are not allowed in trigger transitions",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// Only 'if' conditions are allowed in exit transitions
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0221OnlyIfConditionsAllowedInExitTransitions = new(
            id             : DiagnosticId.Nav0221,
            messageFormat  : "Only 'if' conditions are allowed in exit transitions",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        /// <summary>
        /// Node '{0}' is reached by edges of different modes
        /// </summary>
        public static readonly DiagnosticDescriptor Nav0222Node0IsReachableByDifferentEdgeModes = new(
            id             : DiagnosticId.Nav0222,
            messageFormat  : "Node '{0}' is reached by edges of different modes",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

        // Code Related

        /// <summary>
        /// Identifier expected
        /// </summary>
        public static readonly DiagnosticDescriptor Nav2000IdentifierExpected = new(
            id             : DiagnosticId.Nav2000,
            messageFormat  : "Identifier expected",
            category       : Category,
            defaultSeverity: DiagnosticSeverity.Error
        );

    }

}