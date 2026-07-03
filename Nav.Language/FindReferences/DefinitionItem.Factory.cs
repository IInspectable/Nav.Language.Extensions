#nullable enable

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

    public static DefinitionItem Create(ISymbol symbol,
                                        ImmutableArray<ClassifiedText> textParts,
                                        bool expandedByDefault = true,
                                        string? sortKey = null) {

        return new DefinitionItem(symbol, textParts, expandedByDefault, sortKey);

    }

    public static DefinitionItem CreateTaskDefinitionItem(ITaskDefinitionSymbol taskDefinition) {
        return Create(
            taskDefinition,
            taskDefinition.ToDisplayParts(),
            sortKey: TaskDeclarationSortKey);
    }

    public static DefinitionItem CreateTaskDeclarationItem(ITaskDeclarationSymbol taskDeclaration) {
        return Create(
            taskDeclaration,
            taskDeclaration.ToDisplayParts(),
            sortKey: TaskDeclarationSortKey);
    }

    public static DefinitionItem? CreateInitConnectionPointDefinition(ITaskDefinitionSymbol taskDefinition, bool expandedByDefault = true) {
        return CreateInitConnectionPointDefinition(taskDefinition.AsTaskDeclaration, expandedByDefault);
    }

    public static DefinitionItem? CreateInitConnectionPointDefinition(ITaskDeclarationSymbol? taskDeclaration, bool expandedByDefault = true) {

        var initConnectionPoint = taskDeclaration?.Inits().FirstOrDefault();
        if (initConnectionPoint == null) {
            return null;
        }

        return CreateInitConnectionPointDefinition(initConnectionPoint, expandedByDefault);

    }

    public static DefinitionItem CreateInitConnectionPointDefinition(IInitConnectionPointSymbol initConnectionPoint, bool expandedByDefault = true) {

        return Create(
            initConnectionPoint,
            DisplayPartsBuilder.BuildInitConnectionPointSymbol(initConnectionPoint, neutralName: true),
            expandedByDefault,
            sortKey: InitConnectionPointSortKey);

    }

    public static ImmutableDictionary<Location, DefinitionItem> CreateExitConnectionPointDefinitions(ITaskDefinitionSymbol taskDefinition, bool expandedByDefault = true) {
        return CreateExitConnectionPointDefinitions(taskDefinition.AsTaskDeclaration, expandedByDefault);

    }

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

    public static DefinitionItem CreateExitConnectionPointDefinition(IConnectionPointSymbol exitConnectionPoint, bool expandedByDefault = true) {

        var exitDefinition = Create(
            exitConnectionPoint,
            exitConnectionPoint.ToDisplayParts(),
            expandedByDefault,
            sortKey: ExitConnectionPointSortKey);
        return exitDefinition;
    }

}