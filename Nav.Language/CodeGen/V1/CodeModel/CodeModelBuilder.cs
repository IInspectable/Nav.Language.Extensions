using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Zentrale Fabrik, die aus dem Semantikmodell eines Tasks (<see cref="ITaskDefinitionSymbol"/>) die
/// wiederkehrenden Teil-CodeModels der V1-Generation zusammensetzt. Die dateibezogenen CodeModels
/// (<see cref="WfsBaseCodeModel"/>, <see cref="WfsCodeModel"/>, <see cref="IWfsCodeModel"/>,
/// <see cref="IBeginWfsCodeModel"/>) rufen diese Methoden auf, um ihre Transitions-, Begin-Wrapper-
/// und Parameterlisten zu füllen. Hier sitzt auch die Auswahl der <b>relevanten Task-Knoten</b>
/// (erreichbar / injizierbar), die entscheidet, welche Methoden und Felder überhaupt erzeugt werden.
/// </summary>
sealed class CodeModelBuilder {

    /// <summary>
    /// Baut je <see cref="IInitNodeSymbol"/> des Tasks ein <see cref="InitTransitionCodeModel"/> — die
    /// Quelle der <c>Begin(…)</c>-Methoden und ihrer <c>{Begin}Logic(…)</c>-Weichen im
    /// <c>{Task}WFSBase</c>.
    /// </summary>
    public static IEnumerable<InitTransitionCodeModel> GetInitTransitions(ITaskDefinitionSymbol taskDefinition, TaskCodeInfo taskCodeInfo) {
        return taskDefinition.NodeDeclarations
                             .OfType<IInitNodeSymbol>()
                              // TODO Was ist mit Inits, die keine Outgoings haben, also eigentlich unbenutzt sind?
                             .Select(initNode => InitTransitionCodeModel.FromInitTransition(initNode, taskCodeInfo));
    }
        
    /// <summary>
    /// Ermittelt die Konstruktor-Parameter/Felder, über die dem <c>{Task}WFSBase</c> die Begin-Wrapper
    /// der <b>injizierten</b> Sub-Tasks hereingereicht werden (je Task-Deklaration ein
    /// <c>IBegin{Sub}WFS {sub}</c>-Parameter, nach Namen sortiert). Grundlage sind die implementierten,
    /// injizierbaren Task-Knoten (<see cref="GetImplementedTaskNodes"/>), dedupliziert über ihre
    /// Deklaration.
    /// </summary>
    public static IEnumerable<ParameterCodeModel> GetTaskBeginParameter(ITaskDefinitionSymbol taskDefinition) {

        var usedTaskDeclarations = GetImplementedTaskNodes(taskDefinition)
                                  .Select(taskNode => taskNode.Declaration)
                                  .WhereNotNull()
                                  .Distinct()
                                  .ToImmutableList();
            
        var taskBegins = ParameterCodeModel.GetTaskBeginsAsParameter(usedTaskDeclarations)
                                           .OrderBy(p => p.ParameterName)
                                           .ToImmutableList();
        return taskBegins;
    }

    /// <summary>
    /// Baut je erreichbarem Task-Knoten ein <see cref="BeginWrapperCodeModel"/> — die Quelle der
    /// <c>Begin{Node}(…)</c>-Wrapper-Methoden im <c>{Task}WFSBase</c>, die einen <c>TaskCall</c>
    /// zusammensetzen.
    /// </summary>
    public static IEnumerable<BeginWrapperCodeModel> GetBeginWrappers(ITaskDefinitionSymbol taskDefinition, TaskCodeInfo taskCodeInfo) {
        return GetReachableTaskNodes(taskDefinition)
           .Select(taskNode => BeginWrapperCodeModel.FromTaskNode(taskNode, taskCodeInfo));
    }

    /// <summary>
    /// Baut je erreichbarem, <b>implementiertem</b> Task-Knoten ein <see cref="ExitTransitionCodeModel"/>
    /// — die Quelle der <c>After{Node}(…)</c>-Rücksprungmethoden samt ihrer <c>{After}Logic(…)</c>-Weichen.
    /// <c>[notimplemented]</c>-Knoten werden übersprungen (sie haben keinen Rücksprung).
    /// </summary>
    public static IEnumerable<ExitTransitionCodeModel> GetExitTransitions(ITaskDefinitionSymbol taskDefinition, TaskCodeInfo taskCodeInfo) {
        return GetReachableTaskNodes(taskDefinition)
              .Where(taskNode => !taskNode.CodeNotImplemented())
              .Select(taskNode => ExitTransitionCodeModel.FromTaskNode(taskNode, taskCodeInfo));
    }

    /// <summary>
    /// Die für die Injektion relevanten Task-Knoten: implementiert (nicht <c>[notimplemented]</c>) und
    /// nicht <c>[donotinject]</c> — nur solche Sub-Tasks werden dem <c>{Task}WFSBase</c> als Feld
    /// hereingereicht. Basis von <see cref="GetTaskBeginParameter"/>.
    /// </summary>
    static ImmutableList<ITaskNodeSymbol> GetImplementedTaskNodes(ITaskDefinitionSymbol taskDefinition) {

        var relevantTaskNodes = taskDefinition.NodeDeclarations
                                              .OfType<ITaskNodeSymbol>()
                                              .Where(taskNode => !taskNode.CodeDoNotInject())
                                              .Where(taskNode => !taskNode.CodeNotImplemented())
                                              .Distinct();

        return relevantTaskNodes.ToImmutableList();
    }

    /// <summary>
    /// Die im Graph erreichbaren Task-Knoten (<see cref="INodeSymbol.IsReachable"/>) — Basis von
    /// <see cref="GetBeginWrappers"/> und <see cref="GetExitTransitions"/>: für unerreichbare Knoten
    /// werden weder Begin-Wrapper noch Rücksprünge erzeugt.
    /// </summary>
    static ImmutableList<ITaskNodeSymbol> GetReachableTaskNodes(ITaskDefinitionSymbol taskDefinition) {

        var relevantTaskNodes = taskDefinition.NodeDeclarations
                                              .OfType<ITaskNodeSymbol>()
                                              .Where(taskNode => taskNode.IsReachable())
                                              .Distinct();

        return relevantTaskNodes.ToImmutableList();
    }

    /// <summary>
    /// Baut je Trigger-Transition ein <see cref="TriggerTransitionCodeModel"/> — die Quelle der
    /// <c>On{Trigger}(…)</c>-Methoden samt <c>{Trigger}Logic(…)</c>-Weichen im <c>{Task}WFSBase</c>.
    /// Sortiert nach Länge und dann Name des Trigger-Namens (stabile Emitter-Reihenfolge). Trigger-
    /// Transitionen gelten stets als benutzt (kein Erreichbarkeits-Filter).
    /// </summary>
    public static IEnumerable<TriggerTransitionCodeModel> GetTriggerTransitions(ITaskDefinitionSymbol taskDefinition, TaskCodeInfo taskCodeInfo) {
        // Trigger Transitions sind per Defininition "used"
        return taskDefinition.TriggerTransitions
                             .SelectMany(triggerTransition => TriggerTransitionCodeModel.FromTriggerTransition(taskCodeInfo, triggerTransition))
                             .OrderBy(st => st.TriggerName.Length)
                             .ThenBy(st => st.TriggerName);
    }
        
    /// <summary>
    /// Liefert die im <c>code</c>-Block des Tasks hinterlegten String-Literale (entklammert) — die
    /// wörtlich in die generierte Datei durchgereichten Code-Deklarationen (siehe
    /// <see cref="IBeginWfsCodeModel.CodeDeclarations"/>).
    /// </summary>
    public static IEnumerable<string> GetCodeDeclarations(ITaskDefinitionSymbol taskDefinition) {
        var codeDeclaration = taskDefinition.Syntax.CodeDeclaration;
        if (codeDeclaration == null) {
            yield break;
        }

        foreach (var code in codeDeclaration.GetGetStringLiterals().Select(sl => sl.ToString().Trim('"'))) {
            yield return code;
        }
    }

    /// <summary>
    /// Übersetzt die im <c>[params …]</c> des Tasks deklarierten Parameter in
    /// <see cref="ParameterCodeModel"/>e — die zusätzlichen Konstruktor-Parameter/Felder der
    /// <c>{Task}WFS</c>-Implementierungsklasse.
    /// </summary>
    public static IEnumerable<ParameterCodeModel> GetTaskParameter(ITaskDefinitionSymbol taskDefinition) {
        var code          = GetTaskParameterSyntaxes();
        var taskParameter = ParameterCodeModel.FromParameterSyntaxes(code);
        return taskParameter;

        IEnumerable<ParameterSyntax> GetTaskParameterSyntaxes() {
            var paramList = taskDefinition.Syntax.CodeParamsDeclaration?.ParameterList;
            if(paramList == null) {
                yield break;
            }

            foreach(var p in paramList) {
                yield return p;
            }
        }
    }
    
}