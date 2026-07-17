#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Baut aus einer <see cref="TaskDefinitionSyntax"/> das <see cref="TaskDefinitionSymbol"/> samt
/// Knoten, Kanten und deren Verdrahtung (Einstieg: <see cref="Build"/>). Als
/// <see cref="SyntaxNodeVisitor"/> besucht er erst alle Knoten-Deklarationen und dann die
/// Transitionen — die Quell-/Ziel-Auflösung der Kanten setzt die vollständige Knotentabelle
/// voraus. Task-Namen werden gegen die zuvor vom <see cref="TaskDeclarationSymbolBuilder"/>
/// gebaute Deklarationstabelle aufgelöst.
/// </summary>
sealed class TaskDefinitionSymbolBuilder: SyntaxNodeVisitor {

    readonly IReadOnlySymbolCollection<TaskDeclarationSymbol> _taskDeclarations;
    readonly List<Diagnostic>                                 _diagnostics;

    // Wird in VisitTaskDefinition gesetzt, bevor die Knoten-/Transitions-Visits darauf zugreifen;
    // bleibt die Task-Definition unbenannt, reicht Build den Null-Fall typisiert hinaus.
    TaskDefinitionSymbol _taskDefinition = null!;

    TaskDefinitionSymbolBuilder(IReadOnlySymbolCollection<TaskDeclarationSymbol> taskDeclarations) {
        _taskDeclarations = taskDeclarations;
        _diagnostics      = new List<Diagnostic>();
    }

    /// <summary>
    /// Einstiegs-Visit: legt das <see cref="TaskDefinitionSymbol"/> an und besucht dann in fester
    /// Reihenfolge Knoten-Deklarationen, Transitionen und Exit-Transitionen. Als
    /// <see cref="ITaskDefinitionSymbol.AsTaskDeclaration"/> wird die gleichnamige Deklaration
    /// nur übernommen, wenn ihre Location mit dem Definitions-Bezeichner identisch ist — sie also
    /// die implizite Deklaration genau dieser Definition ist (bei Namenskonflikten bleibt sie
    /// <c>null</c>). Ohne Bezeichner entsteht kein Symbol.
    /// </summary>
    public override void VisitTaskDefinition(TaskDefinitionSyntax taskDefinitionSyntax) {
        var identifier = taskDefinitionSyntax.Identifier;
        var location   = identifier.GetLocation();

        if (identifier.IsMissing || location == null) {
            return;
        }

        var taskName = identifier.ToString();

        var taskDeclaration = _taskDeclarations.TryFindSymbol(taskName);
        if (taskDeclaration?.Location != location) {
            taskDeclaration = null;
        }

        _taskDefinition = new TaskDefinitionSymbol(taskName, location, taskDefinitionSyntax, taskDeclaration);

        // Declarations
        foreach (var nodeDeclarationSyntax in taskDefinitionSyntax.NodeDeclarationBlock.NodeDeclarations) {
            Visit(nodeDeclarationSyntax);
        }

        // Transitions
        foreach (var transitionDefinitionSyntax in taskDefinitionSyntax.TransitionDefinitionBlock.TransitionDefinitions) {
            Visit(transitionDefinitionSyntax);
        }

        // ExitTransitions
        foreach (var transitionDefinitionSyntax in taskDefinitionSyntax.TransitionDefinitionBlock.ExitTransitionDefinitions) {
            Visit(transitionDefinitionSyntax);
        }

    }

    #region Node Declarations

    /// <summary>
    /// Erzeugt das <see cref="InitNodeSymbol"/>: Der deklarierte Symbol-Name ist stets
    /// <c>Init</c> (<see cref="SyntaxFacts.InitKeywordAlt"/>), verortet am
    /// <c>init</c>-Schlüsselwort; ein Bezeichner hinter <c>init</c> wird zum Alias
    /// (<see cref="InitNodeAliasSymbol"/>), dessen Name dann als effektiver Knotenname gilt.
    /// </summary>
    public override void VisitInitNodeDeclaration(InitNodeDeclarationSyntax initNodeDeclarationSyntax) {

        var identifier = initNodeDeclarationSyntax.InitKeyword;
        var taskAlias  = initNodeDeclarationSyntax.Identifier;

        InitNodeAliasSymbol? initNodeAlias = null;
        if (!taskAlias.IsMissing) {
            var aliasName     = taskAlias.ToString();
            var aliasLocation = taskAlias.GetLocation();
            if (aliasLocation != null) {
                initNodeAlias = new InitNodeAliasSymbol(initNodeDeclarationSyntax.SyntaxTree, aliasName, aliasLocation);
            }
        }

        var location = identifier.GetLocation();
        if (location == null) {
            return;
        }

        var decl = new InitNodeSymbol(SyntaxFacts.InitKeywordAlt, location, initNodeDeclarationSyntax, initNodeAlias, _taskDefinition);

        AddNodeDeclaration(decl);
    }

    /// <summary>Erzeugt das <see cref="ExitNodeSymbol"/> zum deklarierten <c>exit</c>-Knoten.</summary>
    public override void VisitExitNodeDeclaration(ExitNodeDeclarationSyntax exitNodeDeclarationSyntax) {

        var identifier = exitNodeDeclarationSyntax.Identifier;
        var location   = identifier.GetLocation();
        var name       = identifier.ToString();

        if (location == null) {
            return;
        }

        var decl = new ExitNodeSymbol(name, location, exitNodeDeclarationSyntax, _taskDefinition);

        AddNodeDeclaration(decl);
    }

    /// <summary>Erzeugt das <see cref="EndNodeSymbol"/> zum <c>end</c>-Knoten; als Name dient das Schlüsselwort selbst.</summary>
    public override void VisitEndNodeDeclaration(EndNodeDeclarationSyntax endNodeDeclarationSyntax) {

        var identifier = endNodeDeclarationSyntax.EndKeyword;
        var location   = identifier.GetLocation();
        var name       = identifier.ToString();

        if (location == null) {
            return;
        }

        var decl = new EndNodeSymbol(name, location, endNodeDeclarationSyntax, _taskDefinition);

        AddNodeDeclaration(decl);
    }

    /// <summary>
    /// Erzeugt das <see cref="TaskNodeSymbol"/> zu einem <c>task</c>-Knoten (z.B.
    /// <c>task Auswahl A1;</c>): löst den Task-Namen gegen die Deklarationstabelle auf, legt für
    /// den optionalen zweiten Bezeichner den Alias (<see cref="TaskNodeAliasSymbol"/>) an und
    /// trägt den Knoten in die <see cref="TaskDeclarationSymbol.References"/> seiner Deklaration
    /// ein.
    /// </summary>
    public override void VisitTaskNodeDeclaration(TaskNodeDeclarationSyntax taskNodeDeclarationSyntax) {

        var taskIdentifier = taskNodeDeclarationSyntax.Identifier;
        var taskAlias      = taskNodeDeclarationSyntax.IdentifierAlias;
        var nodeIdentifier = taskAlias.IsMissing ? taskIdentifier : taskAlias;

        if (nodeIdentifier.IsMissing) {
            // Diesen Fall haben wir, wenn nur "task ;" eingegeben wird. Dafür gibt es aber bereits einen Syntax Fehler.
            return;
        }

        TaskNodeAliasSymbol? taskNodeAlias = null;
        if (!taskAlias.IsMissing) {
            var aliasName     = taskAlias.ToString();
            var aliasLocation = taskAlias.GetLocation();
            if (aliasLocation != null) {
                taskNodeAlias = new TaskNodeAliasSymbol(taskNodeDeclarationSyntax.SyntaxTree, aliasName, aliasLocation);
            }
        }

        var taskName     = taskIdentifier.ToString();
        var taskLocation = taskIdentifier.GetLocation();
        if (taskLocation == null) {
            return;
        }

        var taskDeclaration = _taskDeclarations.TryFindSymbol(taskName);

        var taskNode = new TaskNodeSymbol(taskName, taskLocation, taskNodeDeclarationSyntax, taskNodeAlias, taskDeclaration, _taskDefinition);

        taskNode.Declaration?.References.Add(taskNode);

        AddNodeDeclaration(taskNode);
    }

    /// <summary>Erzeugt das <see cref="DialogNodeSymbol"/> zum deklarierten <c>dialog</c>-Knoten.</summary>
    public override void VisitDialogNodeDeclaration(DialogNodeDeclarationSyntax dialogNodeDeclarationSyntax) {

        var identifier = dialogNodeDeclarationSyntax.Identifier;
        var location   = identifier.GetLocation();
        var name       = identifier.ToString();

        if (location == null) {
            return;
        }

        var decl = new DialogNodeSymbol(name, location, dialogNodeDeclarationSyntax, _taskDefinition);

        AddNodeDeclaration(decl);
    }

    /// <summary>Erzeugt das <see cref="ViewNodeSymbol"/> zum deklarierten <c>view</c>-Knoten.</summary>
    public override void VisitViewNodeDeclaration(ViewNodeDeclarationSyntax viewNodeDeclarationSyntax) {

        var identifier = viewNodeDeclarationSyntax.Identifier;
        var location   = identifier.GetLocation();
        var name       = identifier.ToString();

        if (location == null) {
            return;
        }

        var decl = new ViewNodeSymbol(name, location, viewNodeDeclarationSyntax, _taskDefinition);

        AddNodeDeclaration(decl);
    }

    /// <summary>Erzeugt das <see cref="ChoiceNodeSymbol"/> zum deklarierten <c>choice</c>-Knoten.</summary>
    public override void VisitChoiceNodeDeclaration(ChoiceNodeDeclarationSyntax choiceNodeDeclarationSyntax) {

        var identifier = choiceNodeDeclarationSyntax.Identifier;
        var location   = identifier.GetLocation();
        var name       = identifier.ToString();

        if (location == null) {
            return;
        }

        var decl = new ChoiceNodeSymbol(name, location, choiceNodeDeclarationSyntax, _taskDefinition);

        AddNodeDeclaration(decl);
    }

    /// <summary>
    /// Trägt den Knoten in die Knotentabelle der Definition ein; ist der Name bereits vergeben,
    /// wird stattdessen Nav0022 gemeldet (mit Verweis auf den bestehenden Knoten).
    /// </summary>
    void AddNodeDeclaration(INodeSymbol nodeSymbol) {

        if (_taskDefinition.NodeDeclarations.Contains(nodeSymbol.Name)) {

            var existing = _taskDefinition.NodeDeclarations[nodeSymbol.Name];

            _diagnostics.Add(new Diagnostic(
                                 nodeSymbol.Location,
                                 existing.Location,
                                 DiagnosticDescriptors.Semantic.Nav0022NodeWithName0AlreadyDeclared,
                                 nodeSymbol.Name));
        } else {
            _taskDefinition.NodeDeclarations.Add(nodeSymbol);
        }
    }

    #endregion

    #region Edges (Transitions / ExitTransitions)

    /// <summary>
    /// Verarbeitet eine reguläre Transition (<c>Quelle --&gt; Ziel …;</c>): Die Art des
    /// aufgelösten Quellknotens bestimmt die konkrete Kante — <c>init</c> → Init-, Choice →
    /// Choice-, GUI-Knoten (View/Dialog) → Trigger-Transition. Eine unauflösbare Quelle meldet
    /// Nav0011; Task-, Exit- und End-Knoten sind als Quelle unzulässig (Nav0100/Nav0101/Nav0102).
    /// Sonderfall <c>init</c>: der Init-Knoten ist unter <c>Init</c>
    /// (<see cref="SyntaxFacts.InitKeywordAlt"/>) eingetragen; als Quelle wird auch die
    /// Kleinschreibung <c>init</c> aufgelöst.
    /// </summary>
    public override void VisitTransitionDefinition(TransitionDefinitionSyntax transitionDefinitionSyntax) {

        // Target
        var targetNodeReference = CreateTargetNodeReference(transitionDefinitionSyntax.TargetNode);

        // Edge
        EdgeModeSymbol? edgeMode   = null;
        var             edgeSyntax = transitionDefinitionSyntax.Edge;
        if (edgeSyntax != null) {
            edgeMode = new EdgeModeSymbol(transitionDefinitionSyntax.SyntaxTree, edgeSyntax.ToString(), edgeSyntax.GetLocation(), edgeSyntax.Mode);
        }

        // Source: Der Source Node ist relevant für die Bestimmung der Transition (Init, Choice, Trigger)
        var sourceNodeSyntax = transitionDefinitionSyntax.SourceNode;

        var sourceNode = _taskDefinition.NodeDeclarations.TryFindSymbol(sourceNodeSyntax.Name);

        // Special case "init": Hier ist implizit auch Großschreibung erlaubt
        if (sourceNode == null && sourceNodeSyntax.Name == SyntaxFacts.InitKeyword) {
            sourceNode = _taskDefinition.NodeDeclarations.TryFindSymbol(SyntaxFacts.InitKeywordAlt);
        }

        var sourceNodeLocation = sourceNodeSyntax.GetLocation();

        switch (sourceNode) {
            case null:
                _diagnostics.Add(new Diagnostic(
                                     sourceNodeLocation,
                                     DiagnosticDescriptors.Semantic.Nav0011CannotResolveNode0,
                                     sourceNodeSyntax.Name));
                break;
            case TaskNodeSymbol:
                _diagnostics.Add(new Diagnostic(
                                     sourceNodeLocation,
                                     DiagnosticDescriptors.Semantic.Nav0100TaskNode0MustNotContainLeavingEdges,
                                     sourceNodeSyntax.Name));
                break;
            case ExitNodeSymbol:
                _diagnostics.Add(new Diagnostic(
                                     sourceNodeLocation,
                                     DiagnosticDescriptors.Semantic.Nav0101ExitNodeMustNotContainLeavingEdges));
                break;
            case EndNodeSymbol:
                _diagnostics.Add(new Diagnostic(
                                     sourceNodeLocation,
                                     DiagnosticDescriptors.Semantic.Nav0102EndNodeMustNotContainLeavingEdges));
                break;
            case InitNodeSymbol initNode:
                AddInitTransition(initNode                  : initNode,
                                  transitionDefinitionSyntax: transitionDefinitionSyntax,
                                  sourceNodeSyntax          : sourceNodeSyntax,
                                  sourceNodeLocation        : sourceNodeLocation,
                                  edgeMode                  : edgeMode,
                                  targetNodeReference       : targetNodeReference);
                break;
            case ChoiceNodeSymbol choiceNode:
                AddChoiceTransition(choiceNode                : choiceNode,
                                    transitionDefinitionSyntax: transitionDefinitionSyntax,
                                    sourceNodeSyntax          : sourceNodeSyntax,
                                    sourceNodelocation        : sourceNodeLocation,
                                    edgeMode                  : edgeMode,
                                    targetNodeReference       : targetNodeReference);
                break;
            case DialogNodeSymbol dialogNode:
                AddTriggerTransition(guiNode                   : dialogNode,
                                     transitionDefinitionSyntax: transitionDefinitionSyntax,
                                     sourceNodeSyntax          : sourceNodeSyntax,
                                     sourceNodelocation        : sourceNodeLocation,
                                     edgeMode                  : edgeMode,
                                     targetNodeReference       : targetNodeReference);
                break;
            case ViewNodeSymbol viewNodeNode:
                AddTriggerTransition(guiNode                   : viewNodeNode,
                                     transitionDefinitionSyntax: transitionDefinitionSyntax,
                                     sourceNodeSyntax          : sourceNodeSyntax,
                                     sourceNodelocation        : sourceNodeLocation,
                                     edgeMode                  : edgeMode,
                                     targetNodeReference       : targetNodeReference);
                break;
        }
    }

    /// <summary>
    /// Verarbeitet eine Exit-Transition (<c>TaskKnoten:Exit --&gt; Ziel;</c>): Die Quelle muss ein
    /// Task-Knoten sein; der benannte Exit wird gegen die Exit-Verbindungspunkte der
    /// Task-Deklaration des Quellknotens aufgelöst
    /// (<see cref="ExitConnectionPointReferenceSymbol"/> — dessen Declaration bleibt <c>null</c>,
    /// wenn es dort keinen Exit dieses Namens gibt).
    /// </summary>
    public override void VisitExitTransitionDefinition(ExitTransitionDefinitionSyntax exitTransitionDefinitionSyntax) {

        // Source in Exit Transition muss immer ein Task sein
        var sourceNodeSyntax     = exitTransitionDefinitionSyntax.SourceNode;
        var sourceTaskNodeSymbol = _taskDefinition.NodeDeclarations.TryFindSymbol(sourceNodeSyntax.Name) as ITaskNodeSymbol;
        var sourceNodeReference  = new TaskNodeReferenceSymbol(exitTransitionDefinitionSyntax.SyntaxTree, sourceNodeSyntax.Name, sourceNodeSyntax.GetLocation(), sourceTaskNodeSymbol, NodeReferenceType.Source);

        // ConnectionPoint
        ExitConnectionPointReferenceSymbol? exitConnectionPointReference = null;
        var                                 exitIdentifier               = exitTransitionDefinitionSyntax.ExitIdentifier;
        if (!exitIdentifier.IsMissing && sourceTaskNodeSymbol != null) {

            var exitIdentifierName  = exitIdentifier.ToString();
            var exitConnectionPoint = sourceTaskNodeSymbol.Declaration?.ConnectionPoints.TryFindSymbol(exitIdentifierName) as IExitConnectionPointSymbol;
            var location            = exitIdentifier.GetLocation();

            if (location != null) {
                exitConnectionPointReference = new ExitConnectionPointReferenceSymbol(exitTransitionDefinitionSyntax.SyntaxTree, exitIdentifierName, location, exitConnectionPoint);
            }
        }

        // Target
        var targetNodeReference = CreateTargetNodeReference(exitTransitionDefinitionSyntax.TargetNode);

        // Edge
        EdgeModeSymbol? edgeMode   = null;
        var             edgeSyntax = exitTransitionDefinitionSyntax.Edge;
        if (edgeSyntax != null) {
            edgeMode = new EdgeModeSymbol(exitTransitionDefinitionSyntax.SyntaxTree, edgeSyntax.ToString(), edgeSyntax.GetLocation(), edgeSyntax.Mode);
        }

        AddExitTransition(exitTransitionDefinitionSyntax, sourceNodeReference, exitConnectionPointReference, edgeMode, targetNodeReference);
    }

    /// <summary>
    /// Legt die <see cref="ExitTransition"/> an und verdrahtet sie: Eintrag in
    /// <see cref="TaskDefinitionSymbol.ExitTransitions"/>, bei aufgelöstem Quell-Task-Knoten
    /// dessen <c>Outgoings</c>/<c>References</c>, Zielseite via
    /// <see cref="WireTargetNodeReferences"/>; ein Continuation-Anhang wird zuvor mitgebaut
    /// (<see cref="CreateContinuationTransition"/>).
    /// </summary>
    private void AddExitTransition(ExitTransitionDefinitionSyntax exitTransitionDefinitionSyntax, TaskNodeReferenceSymbol sourceNodeReference, ExitConnectionPointReferenceSymbol? exitConnectionPointReference, EdgeModeSymbol? edgeMode, NodeReferenceSymbol? targetNodeReference) {

        var continuationTransition = CreateContinuationTransition(exitTransitionDefinitionSyntax.ContinuationTransition, exitTransitionDefinitionSyntax.TargetNode);
        var exitTransition         = new ExitTransition(exitTransitionDefinitionSyntax, _taskDefinition, sourceNodeReference, exitConnectionPointReference, edgeMode, targetNodeReference, continuationTransition);

        _taskDefinition.ExitTransitions.Add(exitTransition);

        if (sourceNodeReference.Declaration is TaskNodeSymbol taskNode) {
            taskNode.Outgoings.Add(exitTransition);
            taskNode.References.Add(sourceNodeReference);
        }

        WireTargetNodeReferences(exitTransition);
    }

    /// <summary>
    /// Legt die <see cref="InitTransition"/> an und verdrahtet sie am Quell-Init-Knoten
    /// (<c>Outgoings</c>/<c>References</c>) sowie an der Zielseite
    /// (<see cref="WireTargetNodeReferences"/>).
    /// </summary>
    private void AddInitTransition(InitNodeSymbol initNode, TransitionDefinitionSyntax transitionDefinitionSyntax, SourceNodeSyntax sourceNodeSyntax, Location sourceNodeLocation, EdgeModeSymbol? edgeMode, NodeReferenceSymbol? targetNodeReference) {

        var continuationTransition = CreateContinuationTransition(transitionDefinitionSyntax.ContinuationTransition, transitionDefinitionSyntax.TargetNode);
        var initNodeReference      = new InitNodeReferenceSymbol(sourceNodeSyntax.SyntaxTree, sourceNodeSyntax.Name, sourceNodeLocation, initNode, NodeReferenceType.Source);
        var initTransition         = new InitTransition(transitionDefinitionSyntax, _taskDefinition, initNodeReference, edgeMode, targetNodeReference, continuationTransition);

        _taskDefinition.InitTransitions.Add(initTransition);

        initNode.Outgoings.Add(initTransition);
        initNode.References.Add(initNodeReference);

        WireTargetNodeReferences(initTransition);
    }

    /// <summary>
    /// Legt die <see cref="ChoiceTransition"/> an und verdrahtet sie am Quell-Choice-Knoten
    /// (<c>Outgoings</c>/<c>References</c>) sowie an der Zielseite
    /// (<see cref="WireTargetNodeReferences"/>).
    /// </summary>
    private void AddChoiceTransition(ChoiceNodeSymbol choiceNode, TransitionDefinitionSyntax transitionDefinitionSyntax, SourceNodeSyntax sourceNodeSyntax, Location sourceNodelocation, EdgeModeSymbol? edgeMode, NodeReferenceSymbol? targetNodeReference) {

        var continuationTransition = CreateContinuationTransition(transitionDefinitionSyntax.ContinuationTransition, transitionDefinitionSyntax.TargetNode);
        var choiceNodeReference    = new ChoiceNodeReferenceSymbol(sourceNodeSyntax.SyntaxTree, sourceNodeSyntax.Name, sourceNodelocation, choiceNode, NodeReferenceType.Source);
        var choiceTransition       = new ChoiceTransition(transitionDefinitionSyntax, _taskDefinition, choiceNodeReference, edgeMode, targetNodeReference, continuationTransition);

        _taskDefinition.ChoiceTransitions.Add(choiceTransition);

        choiceNode.Outgoings.Add(choiceTransition);
        choiceNode.References.Add(choiceNodeReference);

        WireTargetNodeReferences(choiceTransition);
    }

    /// <summary>
    /// Legt die <see cref="TriggerTransition"/> eines GUI-Knotens an: baut zunächst deren
    /// Trigger samt zugehöriger Diagnostics (<see cref="TriggerSymbolBuilder"/>) und verdrahtet
    /// dann wie bei den übrigen Kanten Quell- und Zielseite.
    /// </summary>
    private void AddTriggerTransition(IGuiNodeSymbolConstruction guiNode, TransitionDefinitionSyntax transitionDefinitionSyntax, SourceNodeSyntax sourceNodeSyntax, Location sourceNodelocation, EdgeModeSymbol? edgeMode, NodeReferenceSymbol? targetNodeReference) {

        var (triggers, diagnostics) = TriggerSymbolBuilder.Build(transitionDefinitionSyntax);

        _diagnostics.AddRange(diagnostics);

        var continuationTransition = CreateContinuationTransition(transitionDefinitionSyntax.ContinuationTransition, transitionDefinitionSyntax.TargetNode);
        var guiNodeReference       = new GuiNodeReferenceSymbol(sourceNodeSyntax.SyntaxTree, sourceNodeSyntax.Name, sourceNodelocation, guiNode, NodeReferenceType.Source);
        var triggerTransition      = new TriggerTransition(transitionDefinitionSyntax, _taskDefinition, guiNodeReference, edgeMode, targetNodeReference, continuationTransition, triggers);

        _taskDefinition.TriggerTransitions.Add(triggerTransition);

        guiNode.Outgoings.Add(triggerTransition);
        guiNode.References.Add(guiNodeReference);

        WireTargetNodeReferences(triggerTransition);
    }

    /// <summary>
    /// Erzeugt die Zielknoten-Referenz einer Kante (<see cref="CreateNodeReference"/> mit
    /// <see cref="NodeReferenceType.Target"/>).
    /// </summary>
    private NodeReferenceSymbol? CreateTargetNodeReference(TargetNodeSyntax? targetNodeSyntax) {
        return CreateNodeReference(targetNodeSyntax, NodeReferenceType.Target);
    }

    /// <summary>
    /// Erzeugt für den benannten Knoten (<paramref name="nodeSyntax"/>) das passende, typisierte
    /// <see cref="NodeReferenceSymbol"/> (je nach aufgelöster Deklaration Init/Choice/Task/Gui/Exit/End)
    /// mit der angegebenen Referenz-Richtung <paramref name="referenceType"/> — auch die Quellseite
    /// einer Continuation nutzt dies (siehe <see cref="CreateContinuationTransition"/>). Das
    /// deklarationslose <c>cancel</c>-Ziel (E4) wird als eigene, stets unaufgelöste
    /// <see cref="CancelNodeReferenceSymbol"/> gebildet; liefert null, wenn kein Knoten benannt ist.
    /// </summary>
    private NodeReferenceSymbol? CreateNodeReference(TargetNodeSyntax? nodeSyntax, NodeReferenceType referenceType) {

        if (nodeSyntax == null) {
            return null;
        }

        // cancel ist ein Kantenziel OHNE Deklaration (E4): "cancel" steht in keiner Knotentabelle, die
        // Namensauflösung greift also nicht. Muss deshalb VOR TryFindSymbol stehen und liefert eine eigene,
        // stets unaufgelöste Referenz — so ist das cancel-Ziel als eigener Fall erkennbar und von Nav0011
        // (unauflösbarer Name) abgegrenzt.
        if (nodeSyntax is CancelTargetNodeSyntax) {
            return new CancelNodeReferenceSymbol(nodeSyntax.SyntaxTree, nodeSyntax.Name, nodeSyntax.GetLocation(), referenceType);
        }

        var nodeDeclaration = _taskDefinition.NodeDeclarations.TryFindSymbol(nodeSyntax.Name);
        var nodeLocation    = nodeSyntax.GetLocation();

        NodeReferenceSymbol nodeReference;
        switch (nodeDeclaration) {
            case IInitNodeSymbol initNode:
                nodeReference = new InitNodeReferenceSymbol(nodeSyntax.SyntaxTree, nodeSyntax.Name, nodeLocation, initNode, referenceType);
                break;
            case IChoiceNodeSymbol choiceNode:
                nodeReference = new ChoiceNodeReferenceSymbol(nodeSyntax.SyntaxTree, nodeSyntax.Name, nodeLocation, choiceNode, referenceType);
                break;
            case ITaskNodeSymbol taskNode:
                nodeReference = new TaskNodeReferenceSymbol(nodeSyntax.SyntaxTree, nodeSyntax.Name, nodeLocation, taskNode, referenceType);
                break;
            case IGuiNodeSymbol guiNode:
                nodeReference = new GuiNodeReferenceSymbol(nodeSyntax.SyntaxTree, nodeSyntax.Name, nodeLocation, guiNode, referenceType);
                break;
            case IExitNodeSymbol exitNode:
                nodeReference = new ExitNodeReferenceSymbol(nodeSyntax.SyntaxTree, nodeSyntax.Name, nodeLocation, exitNode, referenceType);
                break;
            case IEndNodeSymbol endNode:
                nodeReference = new EndNodeReferenceSymbol(nodeSyntax.SyntaxTree, nodeSyntax.Name, nodeLocation, endNode, referenceType);
                break;
            default:
                nodeReference = new NodeReferenceSymbol(nodeSyntax.SyntaxTree, nodeSyntax.Name, nodeLocation, nodeDeclaration, referenceType);
                break;
        }

        return nodeReference;
    }

    /// <summary>
    /// Baut zum optionalen Continuation-Anhang (<c>… o-^ Task</c> / <c>… --^ Task</c>, ab Sprachversion 2)
    /// einer Transition die <see cref="ContinuationTransition"/> im Semantic Model: Quelle ist der tragende
    /// GUI-Knoten (<paramref name="carrierNodeSyntax"/>, der Zielknoten der umgebenden Transition), Ziel der
    /// Folge-Task; das Ziel wird in den Referenzgraphen eingehängt. Die strukturelle Gültigkeit (Quelle = GUI-,
    /// Ziel = Task-Knoten) prüfen eigene Analyzer. Liefert null, wenn keine Continuation vorhanden ist.
    /// </summary>
    private ContinuationTransition? CreateContinuationTransition(ContinuationTransitionSyntax? continuationTransitionSyntax, TargetNodeSyntax? carrierNodeSyntax) {

        if (continuationTransitionSyntax == null) {
            return null;
        }

        var sourceNodeReference = CreateNodeReference(carrierNodeSyntax, NodeReferenceType.Source);
        var targetNodeReference = CreateNodeReference(continuationTransitionSyntax.TargetNode, NodeReferenceType.Target);

        EdgeModeSymbol? edgeMode   = null;
        var             edgeSyntax = continuationTransitionSyntax.Edge;
        if (edgeSyntax != null) {
            edgeMode = new EdgeModeSymbol(continuationTransitionSyntax.SyntaxTree, edgeSyntax.ToString(), edgeSyntax.GetLocation(), edgeSyntax.Mode);
        }

        var continuationTransition = new ContinuationTransition(continuationTransitionSyntax, _taskDefinition, sourceNodeReference, edgeMode, targetNodeReference);

        WireTargetNodeReferences(continuationTransition);

        return continuationTransition;
    }

    /// <summary>
    /// Hängt die Kante an ihrem Zielknoten ein (<c>Incomings</c> + <c>References</c>).
    /// Unauflösbare Ziele und Init-Knoten als Ziel werden bewusst nicht verdrahtet — die
    /// zugehörigen Diagnosen (Nav0011 bzw. Nav0103) entstehen an anderer Stelle.
    /// </summary>
    private static void WireTargetNodeReferences(IEdge edge) {

        //==============================
        // Target
        //==============================
        var targetReference = edge.TargetReference;
        switch (targetReference?.Declaration) {
            case null:
                // Nav0011CannotResolveNode0
                break;
            case InitNodeSymbol:
                // Nav0103InitNodeMustNotContainIncomingEdges
                break;
            case ITargetNodeSymbolConstruction targetNode:
                targetNode.Incomings.Add(edge);
                targetNode.References.Add(targetReference);
                break;
        }
    }

    #endregion

    /// <summary>
    /// Baut das <see cref="TaskDefinitionSymbol"/> zur übergebenen Definition;
    /// <c>TaskDefinition</c> ist <c>null</c>, wenn die Definition keinen Bezeichner trägt.
    /// </summary>
    /// <param name="taskDefinitionSyntax">Die zu bindende <c>task</c>-Definition.</param>
    /// <param name="taskDeclarations">Die Deklarationstabelle der Datei (inklusive der aus
    /// Includes stammenden Deklarationen) zur Auflösung von Task-Knoten.</param>
    public static (
        TaskDefinitionSymbol? TaskDefinition,
        List<Diagnostic> Diagnostics)
        Build(TaskDefinitionSyntax taskDefinitionSyntax, IReadOnlySymbolCollection<TaskDeclarationSymbol> taskDeclarations) {

        var builder = new TaskDefinitionSymbolBuilder(taskDeclarations);
        builder.Visit(taskDefinitionSyntax);
        return (builder._taskDefinition, builder._diagnostics);
    }

}
