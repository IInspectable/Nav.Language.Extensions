#region Using Directives

using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Extension.Images;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.NavigationBar; 

/// <summary>
/// Baut die Einträge der Task-Combobox der Navigationsleiste: Als <see cref="SymbolVisitor"/> erzeugt er
/// aus den Task-Definitionen und (nicht schon als Definition erfassten) Task-Deklarationen des Modells je
/// einen <see cref="NavigationBarItem"/>, nach Startposition sortiert.
/// </summary>
class NavigationBarTaskItemBuilder : SymbolVisitor {

    protected NavigationBarTaskItemBuilder() {
        NavigationItems = new List<NavigationBarItem>();
        MemberItems     = new List<NavigationBarItem>();
    }

    /// <summary>Die gesammelten Task-Einträge (obere Ebene der Combobox).</summary>
    public List<NavigationBarItem> NavigationItems { get; }
    /// <summary>Puffer der Member-Einträge (untergeordnete Combobox) der gerade besuchten Task-Definition.</summary>
    public List<NavigationBarItem> MemberItems     { get; }

    /// <summary>Erzeugt die sortierte Liste der Task-Einträge zum Modell (leere Liste, wenn keins vorliegt).</summary>
    public static ImmutableList<NavigationBarItem> Build(CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot) {

        var codeGenerationUnit = codeGenerationUnitAndSnapshot?.CodeGenerationUnit;
        if(codeGenerationUnit == null) {
            return ImmutableList<NavigationBarItem>.Empty;
        }

        var builder = new NavigationBarTaskItemBuilder();

        foreach (var symbol in codeGenerationUnit.TaskDefinitions) {
            builder.Visit(symbol);
        }

        foreach (var symbol in codeGenerationUnit.TaskDeclarations) {
            builder.Visit(symbol);
        }

        var items = builder.NavigationItems
                           .OrderBy(ni => ni.Start)
                           .ToImmutableList();

        return items;
    }

    /// <summary>Fügt für eine Task-Definition einen Eintrag mit Definitions-Glyph hinzu (samt gepufferter Member).</summary>
    public override void VisitTaskDefinitionSymbol(ITaskDefinitionSymbol taskDefinitionSymbol) {
        #if ShowMemberCombobox
            foreach (var symbol in taskDefinitionSymbol.Transitions.SelectMany(trans => trans.Symbols())) {
                Visit(symbol);
            }
        #endif

        NavigationItems.Add(new NavigationBarItem(
                                displayName    : taskDefinitionSymbol.Name, 
                                imageMoniker   : ImageMonikers.TaskDefinition, 
                                location       : taskDefinitionSymbol.Syntax.GetLocation(), 
                                navigationPoint: taskDefinitionSymbol.Location.Start,
                                children       : MemberItems.ToImmutableList()));

        MemberItems.Clear();
    }

    /// <summary>
    /// Fügt für eine reine Task-Deklaration (<c>taskref</c>) einen Eintrag mit Deklarations-Glyph hinzu;
    /// Deklarationen, die zugleich Definition sind, werden übersprungen (bereits von
    /// <see cref="VisitTaskDefinitionSymbol"/> erfasst).
    /// </summary>
    public override void VisitTaskDeclarationSymbol(ITaskDeclarationSymbol taskDeclarationSymbol) {

        // Haben wir bereits in Form der Taskdefinition abgefrühstückt
        // => Jede Taskdefinition ist auch eine Deklaration
        if(taskDeclarationSymbol.Origin == TaskDeclarationOrigin.TaskDefinition) {
            return;
        }

        NavigationItems.Add(new NavigationBarItem(
                                displayName    : taskDeclarationSymbol.Name, 
                                imageMoniker   : ImageMonikers.TaskDeclaration, 
                                location       : taskDeclarationSymbol.Syntax?.GetLocation(), 
                                navigationPoint: taskDeclarationSymbol.Location.Start));
    }

    #if ShowMemberCombobox
        public override void VisitSignalTriggerSymbol(ISignalTriggerSymbol signalTriggerSymbol) {
            MemberItems.Add(new NavigationBarItem(signalTriggerSymbol.Name, NavigationBarImages.Index.TriggerSymbol, signalTriggerSymbol.Transition.Location, signalTriggerSymbol.Start));
        }
    #endif
}