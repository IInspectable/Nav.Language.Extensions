#region Using Directives

using System.Collections.Immutable;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

public partial class DefinitionItem {

    private const string TaskDeclarationSortKey     = "a";
    private const string InitConnectionPointSortKey = "b";
    private const string ExitConnectionPointSortKey = "c";

    /// <summary>
    /// Erzeugt eine reine Text-Definition ohne zugrunde liegendes Symbol — etwa für eine Überschrift
    /// oder Meldung im Ergebnisbaum.
    /// </summary>
    /// <param name="text">Der anzuzeigende Text.</param>
    public static DefinitionItem CreateSimpleItem(string text) {
        return SimpleTextDefinition.Create(text);
    }

    class SimpleTextDefinition: DefinitionItem {

        SimpleTextDefinition(ImmutableArray<ClassifiedText> textParts):
            base(null, textParts: textParts, expandedByDefault: true, sortKey: null) {
        }

        public static SimpleTextDefinition Create(string text) {

            var textParts = new[] {ClassifiedTexts.Text(text)}.ToImmutableArray();

            return new SimpleTextDefinition(textParts);
        }

    }

    /// <summary>
    /// Erzeugt eine Definition zu <paramref name="symbol"/> mit vorgegebenem Anzeigetext — die
    /// allgemeine Factory, auf die die spezialisierten <c>Create…</c>-Methoden zurückgreifen.
    /// </summary>
    /// <param name="symbol">Das zugrunde liegende Symbol.</param>
    /// <param name="textParts">Der klassifizierte Anzeigetext.</param>
    /// <param name="expandedByDefault">Ob der Knoten standardmäßig aufgeklappt dargestellt wird.</param>
    /// <param name="sortKey">Der Sortierschlüssel oder <c>null</c> (dann leer).</param>
    public static DefinitionItem Create(ISymbol symbol,
                                        ImmutableArray<ClassifiedText> textParts,
                                        bool expandedByDefault = true,
                                        string? sortKey = null) {

        return new DefinitionItem(symbol, textParts, expandedByDefault, sortKey);

    }

    /// <summary>Erzeugt die Definition zu einer Task-<em>Definition</em> (<c>task</c>).</summary>
    /// <param name="taskDefinition">Das Symbol der Task-Definition.</param>
    public static DefinitionItem CreateTaskDefinitionItem(ITaskDefinitionSymbol taskDefinition) {
        return Create(
            taskDefinition,
            taskDefinition.ToDisplayParts(),
            sortKey: TaskDeclarationSortKey);
    }

    /// <summary>Erzeugt die Definition zu einer Task-<em>Deklaration</em> (<c>taskref</c>).</summary>
    /// <param name="taskDeclaration">Das Symbol der Task-Deklaration.</param>
    public static DefinitionItem CreateTaskDeclarationItem(ITaskDeclarationSymbol taskDeclaration) {
        return Create(
            taskDeclaration,
            taskDeclaration.ToDisplayParts(),
            sortKey: TaskDeclarationSortKey);
    }

    /// <summary>
    /// Erzeugt die Definition zum Init-Verbindungspunkt der Task-Definition, oder <c>null</c>, wenn
    /// die Task keinen Init besitzt.
    /// </summary>
    /// <param name="taskDefinition">Die Task-Definition, deren Init-Verbindungspunkt gesucht wird.</param>
    /// <param name="expandedByDefault">Ob der Knoten standardmäßig aufgeklappt dargestellt wird.</param>
    public static DefinitionItem? CreateInitConnectionPointDefinition(ITaskDefinitionSymbol taskDefinition, bool expandedByDefault = true) {
        return CreateInitConnectionPointDefinition(taskDefinition.AsTaskDeclaration, expandedByDefault);
    }

    /// <summary>
    /// Erzeugt die Definition zum Init-Verbindungspunkt der Task-Deklaration, oder <c>null</c>, wenn
    /// <paramref name="taskDeclaration"/> <c>null</c> ist oder keinen Init besitzt.
    /// </summary>
    /// <param name="taskDeclaration">Die Task-Deklaration, deren Init-Verbindungspunkt gesucht wird.</param>
    /// <param name="expandedByDefault">Ob der Knoten standardmäßig aufgeklappt dargestellt wird.</param>
    public static DefinitionItem? CreateInitConnectionPointDefinition(ITaskDeclarationSymbol? taskDeclaration, bool expandedByDefault = true) {

        var initConnectionPoint = taskDeclaration?.Inits().FirstOrDefault();
        if (initConnectionPoint == null) {
            return null;
        }

        return CreateInitConnectionPointDefinition(initConnectionPoint, expandedByDefault);

    }

    /// <summary>
    /// Erzeugt die Definition zu einem konkreten Init-Verbindungspunkt (mit neutralisiertem Namen in
    /// der Anzeige).
    /// </summary>
    /// <param name="initConnectionPoint">Der Init-Verbindungspunkt.</param>
    /// <param name="expandedByDefault">Ob der Knoten standardmäßig aufgeklappt dargestellt wird.</param>
    public static DefinitionItem CreateInitConnectionPointDefinition(IInitConnectionPointSymbol initConnectionPoint, bool expandedByDefault = true) {

        return Create(
            initConnectionPoint,
            DisplayPartsBuilder.BuildInitConnectionPointSymbol(initConnectionPoint, neutralName: true),
            expandedByDefault,
            sortKey: InitConnectionPointSortKey);

    }

    /// <summary>
    /// Erzeugt die Definitionen zu allen Exit-Verbindungspunkten der Task-Definition, indiziert nach
    /// ihrer <see cref="Location"/>.
    /// </summary>
    /// <param name="taskDefinition">Die Task-Definition, deren Exits gesammelt werden.</param>
    /// <param name="expandedByDefault">Ob die Knoten standardmäßig aufgeklappt dargestellt werden.</param>
    public static ImmutableDictionary<Location, DefinitionItem> CreateExitConnectionPointDefinitions(ITaskDefinitionSymbol taskDefinition, bool expandedByDefault = true) {
        return CreateExitConnectionPointDefinitions(taskDefinition.AsTaskDeclaration, expandedByDefault);

    }

    /// <summary>
    /// Erzeugt die Definitionen zu allen Exit-Verbindungspunkten der Task-Deklaration, indiziert nach
    /// ihrer <see cref="Location"/>; leer, wenn <paramref name="taskDeclaration"/> <c>null</c> ist.
    /// </summary>
    /// <param name="taskDeclaration">Die Task-Deklaration, deren Exits gesammelt werden.</param>
    /// <param name="expandedByDefault">Ob die Knoten standardmäßig aufgeklappt dargestellt werden.</param>
    public static ImmutableDictionary<Location, DefinitionItem> CreateExitConnectionPointDefinitions(ITaskDeclarationSymbol? taskDeclaration, bool expandedByDefault = true) {

        var defs = ImmutableDictionary<Location, DefinitionItem>.Empty;

        if (taskDeclaration == null) {
            return defs;
        }

        foreach (var exitConnectionPoint in taskDeclaration.Exits()) {
            var exitDefinition = CreateExitConnectionPointDefinition(exitConnectionPoint, expandedByDefault);
            defs = defs.Add(exitConnectionPoint.Location, exitDefinition);
        }

        return defs;
    }

    /// <summary>Erzeugt die Definition zu einem einzelnen Exit-Verbindungspunkt.</summary>
    /// <param name="exitConnectionPoint">Der Exit-Verbindungspunkt.</param>
    /// <param name="expandedByDefault">Ob der Knoten standardmäßig aufgeklappt dargestellt wird.</param>
    public static DefinitionItem CreateExitConnectionPointDefinition(IConnectionPointSymbol exitConnectionPoint, bool expandedByDefault = true) {

        var exitDefinition = Create(
            exitConnectionPoint,
            exitConnectionPoint.ToDisplayParts(),
            expandedByDefault,
            sortKey: ExitConnectionPointSortKey);
        return exitDefinition;
    }

}