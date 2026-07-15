#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

/// <summary>
/// Führt vor dem Ziel einer Transition eine neue Choice ein: Der Fix schaltet einen frisch deklarierten
/// Choice-Knoten zwischen die Quelle und das bisherige Ziel, indem er die vorhandene Ziel-Referenz auf
/// die Choice umbiegt und eine neue Transition von der Choice zum ursprünglichen Ziel ergänzt. Angeboten
/// an einer Ziel-Knotenreferenz (siehe <see cref="CanApplyFix"/>).
/// </summary>
public class IntroduceChoiceCodeFix: RefactoringCodeFix {

    internal IntroduceChoiceCodeFix(INodeReferenceSymbol nodeReference, CodeFixContext context)
        : base(context) {
        NodeReference = nodeReference ?? throw new ArgumentNullException(nameof(nodeReference));
    }

    /// <summary>Die Knotenreferenz (das Transitionsziel), vor der die Choice eingeführt wird.</summary>
    public INodeReferenceSymbol   NodeReference  { get; }
    /// <summary>Die Task-Definition, in der die neue Choice angelegt wird, oder <c>null</c>, wenn die Referenz unaufgelöst ist.</summary>
    public ITaskDefinitionSymbol? ContainingTask => NodeReference.Declaration?.ContainingTask;

    /// <summary>Der Anzeigename des Fixes: „Introduce Choice".</summary>
    public override string        Name         => "Introduce Choice";
    /// <summary>Immer <see cref="CodeFixImpact.None"/> — eine Choice bleibt Nav-intern.</summary>
    public override CodeFixImpact Impact       => CodeFixImpact.None;
    /// <summary>Der Bereich, an dem der Fix angeboten wird — die Location der <see cref="NodeReference"/>.</summary>
    public override TextExtent?   ApplicableTo => NodeReference.Location.Extent;
    /// <summary>Priorität <see cref="CodeFixPrio.Medium"/>.</summary>
    public override CodeFixPrio   Prio         => CodeFixPrio.Medium;

    /// <summary>
    /// Schlägt einen im umgebenden Task noch freien Choice-Namen vor: ausgehend von
    /// <c>Choice_&lt;Zielname&gt;</c> wird bei Kollision eine laufende Nummer angehängt, bis
    /// <see cref="ValidateChoiceName"/> keinen Fehler mehr meldet.
    /// </summary>
    public string SuggestChoiceName() {
        string baseName   = $"Choice_{NodeReference.Name}";
        string choiceName = baseName;
        int    number     = 1;
        while (!String.IsNullOrEmpty(ValidateChoiceName(choiceName))) {
            choiceName = $"{baseName}{number++}";
        }

        return choiceName;
    }

    /// <summary>
    /// Ob der Fix anwendbar ist: nur an einem Transitionsziel
    /// (<see cref="NodeReferenceType.Target"/>) mit aufgelöster Deklaration und einer Quelle sowie einem
    /// Kantenmodus an der Edge — nur dann lässt sich die Choice sinnvoll dazwischenschalten.
    /// </summary>
    internal bool CanApplyFix() {

        return NodeReference.NodeReferenceType    == NodeReferenceType.Target &&
               NodeReference.Declaration          != null                     &&
               NodeReference.Edge.SourceReference != null                     &&
               NodeReference.Edge.EdgeMode        != null;
    }

    /// <summary>
    /// Prüft <paramref name="choiceName"/> als neuen Choice-Namen gegen den umgebenden Task und liefert
    /// eine Fehlermeldung bei Unzulässigkeit (etwa Namenskollision), sonst <c>null</c>.
    /// </summary>
    public string? ValidateChoiceName(string? choiceName) {
        return ContainingTask.ValidateNewNodeName(choiceName);
    }

    /// <summary>
    /// Berechnet die Textänderungen zum Einführen der Choice mit dem Namen <paramref name="choiceName"/>:
    /// eine neue <c>choice</c>-Deklaration bei der Ziel-Knotendeklaration, das Umbiegen der vorhandenen
    /// Ziel-Referenz auf die Choice, das Angleichen des Kantenmodus auf <c>--&gt;</c> und eine neue
    /// Transition von der Choice zum bisherigen Ziel. Wirft eine <see cref="InvalidOperationException"/>,
    /// wenn <see cref="CanApplyFix"/> nicht erfüllt ist, bzw. eine <see cref="ArgumentException"/> bei
    /// unzulässigem <paramref name="choiceName"/>.
    /// </summary>
    public IList<TextChange> GetTextChanges(string? choiceName) {

        if (!CanApplyFix()) {
            throw new InvalidOperationException();
        }

        choiceName = choiceName?.Trim() ?? String.Empty;

        var validationMessage = ValidateChoiceName(choiceName);
        if (!String.IsNullOrEmpty(validationMessage)) {
            throw new ArgumentException(validationMessage, nameof(choiceName));
        }

        var edge       = NodeReference.Edge;
        var edgeMode   = edge.EdgeMode;
        // CanApplyFix (oben geprüft) garantiert NodeReference.Declaration != null.
        var nodeSymbol = NodeReference.Declaration!;

        var nodeDeclarationLine = SyntaxTree.SourceText.GetTextLineAtPosition(nodeSymbol.Start);
        var nodeTransitionLine  = SyntaxTree.SourceText.GetTextLineAtPosition(NodeReference.End);

        var choiceDeclaration = $"{GetIndentAsSpaces(nodeDeclarationLine)}{SyntaxFacts.ChoiceKeyword}{WhiteSpaceBetweenChoiceKeywordAndIdentifier(nodeSymbol)}{choiceName}{SyntaxFacts.Semicolon}";
        var choiceTransition  = $"{GetIndentAsSpaces(nodeTransitionLine)}{choiceName}{WhiteSpaceBetweenSourceAndEdgeMode(edge, choiceName)}{edge.EdgeMode?.Name}{WhiteSpaceBetweenEdgeModeAndTarget(edge)}{NodeReference.Name}{SyntaxFacts.Semicolon}";

        var textChanges = new List<TextChange>();
        // Die Choice Deklaration: choice NeueChoice;
        textChanges.AddRange(GetInsertChanges(nodeDeclarationLine.Extent.End, $"{choiceDeclaration}{Context.TextEditorSettings.NewLine}"));
        // Die Node Reference wird nun umgebogen auf die choice
        textChanges.AddRange(GetRenameSymbolChanges(NodeReference, choiceName));
        // Die Edge der choice ist immer '-->'
        textChanges.AddRange(GetRenameSymbolChanges(edgeMode, SyntaxFacts.GoToEdgeKeyword));
        // Die neue choice Transition 
        textChanges.AddRange(GetInsertChanges(nodeTransitionLine.Extent.End, $"{choiceTransition}{Context.TextEditorSettings.NewLine}"));

        return textChanges;
    }

    string WhiteSpaceBetweenChoiceKeywordAndIdentifier(INodeSymbol sampleNode) {

        var offset = ColumnsBetweenKeywordAndIdentifier(sampleNode, newKeyword: SyntaxFacts.ChoiceKeyword);
        return new String(' ', offset);
    }

}