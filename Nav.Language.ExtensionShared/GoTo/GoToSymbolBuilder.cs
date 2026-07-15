#region Using Directives

using System.Linq;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Language.CodeGen;
using Pharmatechnik.Nav.Language.GoTo;
using Pharmatechnik.Nav.Language.Extension.Images;
using Pharmatechnik.Nav.Language.Extension.GoToLocation;
using Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoTo; 

/// <summary>
/// Baut zu einem Nav-Symbol den <see cref="GoToTag"/> mit dessen Sprungzielen — die eigentliche
/// Sprungziel-Logik hinter dem <see cref="GoToTagger"/>. Als <see cref="SymbolVisitor{T}"/> entscheidet er
/// je Symbolart, welche Ziele angeboten werden: Sprünge in den generierten C#-Code (über die
/// <c>*CodeInfo</c>-Modelle und die Location-Info-Provider aus <c>GoToLocation.Provider</c>) und/oder
/// Nav→Nav-Sprünge (über das Sprungziel aus dem geteilten Engine-Kern
/// <see cref="Pharmatechnik.Nav.Language.GoTo.NavGoToService"/>). Symbolarten ohne Sprungziel liefern
/// <c>null</c>.
/// </summary>
sealed class GoToSymbolBuilder : SymbolVisitor<TagSpan<GoToTag>> {

    readonly CodeGenerationUnitAndSnapshot _codeGenerationUnitAndSnapshot;
    readonly ITextBuffer                   _textBuffer;

    GoToSymbolBuilder(CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot, ITextBuffer textBuffer) {
        _codeGenerationUnitAndSnapshot = codeGenerationUnitAndSnapshot;
        _textBuffer                    = textBuffer;
    }

    /// <summary>
    /// Baut den <see cref="GoToTag"/> für <paramref name="source"/>; liefert <c>null</c>, wenn das Symbol
    /// kein Sprungziel besitzt.
    /// </summary>
    public static TagSpan<GoToTag> Build(CodeGenerationUnitAndSnapshot codeGenerationUnitAndSnapshot, ISymbol source, ITextBuffer textBuffer) {
        var builder = new GoToSymbolBuilder(codeGenerationUnitAndSnapshot, textBuffer);
        return builder.Visit(source);
    }

    /// <summary>
    /// Sprung von einer <c>include</c>-Direktive in die eingebundene <c>.nav</c>-Datei (Nav→Nav); ohne
    /// auflösbares Ziel entfällt der Tag.
    /// </summary>
    public override TagSpan<GoToTag> VisitIncludeSymbol(IIncludeSymbol includeSymbol) {

        var target = ResolveNavTarget(includeSymbol);
        if (target == null) {
            return null;
        }

        return CreateGoToLocationTagSpan(includeSymbol.Location,
                                         LocationInfo.FromLocation(
                                             location    : target,
                                             displayName : includeSymbol.FileName,
                                             imageMoniker: ImageMonikers.Include));
    }

    /// <summary>
    /// Sprung von einer Task-Definition in die generierte Task-Deklaration im C#-Code (über
    /// <see cref="TaskDeclarationLocationInfoProvider"/>); bei fehlendem Bezeichner entfällt der Tag.
    /// </summary>
    public override TagSpan<GoToTag> VisitTaskDefinitionSymbol(ITaskDefinitionSymbol taskDefinitionSymbol) {

        if(taskDefinitionSymbol.Syntax.Identifier.IsMissing) {
            return null;
        }

        var codeModel = TaskCodeInfo.FromTaskDefinition(taskDefinitionSymbol);
        var provider  = new TaskDeclarationLocationInfoProvider(_textBuffer, codeModel);
            
        return CreateTagSpan(taskDefinitionSymbol.Location, provider);
    }

    /// <summary>
    /// Sprung von einer (eigenständigen, nicht included und nicht aus einer Task-Definition stammenden)
    /// Task-Deklaration in das generierte <c>IBegin…</c>-Interface im C#-Code (über
    /// <see cref="TaskIBeginInterfaceDeclarationLocationInfoProvider"/>).
    /// </summary>
    public override TagSpan<GoToTag> VisitTaskDeclarationSymbol(ITaskDeclarationSymbol taskDeclarationSymbol) {

        if (taskDeclarationSymbol.IsIncluded || taskDeclarationSymbol.Origin ==TaskDeclarationOrigin.TaskDefinition) {
            return null;
        }

        var codeModel = TaskDeclarationCodeInfo.FromTaskDeclaration(taskDeclarationSymbol);
        var provider  = new TaskIBeginInterfaceDeclarationLocationInfoProvider(_textBuffer, codeModel);

        return CreateTagSpan(taskDeclarationSymbol.Location, provider);
    }

    /// <summary>
    /// Sprung von einem Task-Knoten zu seiner Task-Deklaration (Nav→Nav); ohne aufgelöste Deklaration
    /// entfällt der Tag.
    /// </summary>
    public override TagSpan<GoToTag> VisitTaskNodeSymbol(ITaskNodeSymbol taskNodeSymbol) {

        if (taskNodeSymbol.Declaration == null) {
            return null;
        }

        var target = ResolveNavTarget(taskNodeSymbol);

        return CreateGoToLocationTagSpan(taskNodeSymbol.Location,
                                         LocationInfo.FromLocation(
                                             location    : target,
                                             displayName : $"Task {taskNodeSymbol.Declaration.Name}",
                                             imageMoniker: ImageMonikers.FromSymbol(taskNodeSymbol)));
    }

    /// <summary>
    /// Sprung von einer Knoten-Referenz zur zugehörigen Knoten-Deklaration (Nav→Nav). Ergänzt zusätzlich
    /// die Sprungziele, die der Besuch der Deklaration selbst liefert (etwa deren C#-Ziele), sodass beide
    /// über dasselbe Tag erreichbar sind.
    /// </summary>
    public override TagSpan<GoToTag> VisitNodeReferenceSymbol(INodeReferenceSymbol nodeReferenceSymbol) {

        if (nodeReferenceSymbol.Declaration == null) {
            return null;
        }

        var target = ResolveNavTarget(nodeReferenceSymbol);

        var tagSpan = CreateGoToLocationTagSpan(nodeReferenceSymbol.Location,
                                                LocationInfo.FromLocation(
                                                    location    : target,
                                                    displayName : "Node Declaration",
                                                    imageMoniker: ImageMonikers.GoToNodeDeclaration));

        var nodeTagSpan = Visit(nodeReferenceSymbol.Declaration);
        if(nodeTagSpan !=null && nodeTagSpan.Tag.Provider.Any()) {
            tagSpan.Tag.Provider.AddRange(nodeTagSpan.Tag.Provider);
        }
     
        return tagSpan;
    }

    /// <summary>
    /// Bietet für eine Exit-Verbindungspunkt-Referenz <b>zwei</b> Sprungziele an: in die generierte Exit-
    /// Deklaration im C#-Code (über <see cref="TaskExitDeclarationLocationInfoProvider"/>) und zur
    /// Exit-Definition im Nav-Code (Nav→Nav, Ziel aus dem Engine-Kern). Ohne aufgelöste Deklaration
    /// entfällt der Tag.
    /// </summary>
    public override TagSpan<GoToTag> VisitExitConnectionPointReferenceSymbol(IExitConnectionPointReferenceSymbol exitConnectionPointReferenceSymbol) {

        if (exitConnectionPointReferenceSymbol.Declaration == null) {
            return null;
        }

        var target = ResolveNavTarget(exitConnectionPointReferenceSymbol);

        // GoTo Exit Declaration (Sprung in den generierten C#-Code — bleibt VS-seitig)
        var codeModel = TaskExitCodeInfo.FromConnectionPointReference(exitConnectionPointReferenceSymbol);
        var provider  = new TaskExitDeclarationLocationInfoProvider(_textBuffer, codeModel);
        var tagSpan   = CreateTagSpan(exitConnectionPointReferenceSymbol.Location, provider);

        // GoTo Exit Definition (Nav→Nav — Ziel aus dem Engine-Kern)
        var defProvider = new SimpleLocationInfoProvider(LocationInfo.FromLocation(
                                                             target,
                                                             $"Exit {exitConnectionPointReferenceSymbol.Name}",
                                                             ImageMonikers.ExitConnectionPoint));

        tagSpan.Tag.Provider.Add(defProvider);

        return tagSpan;
    }

    /// <summary>
    /// Sprung von einem Init-Knoten <b>ohne</b> Alias in die generierte <c>Begin…</c>-Deklaration im
    /// C#-Code (über <see cref="TaskBeginDeclarationLocationInfoProvider"/>); mit Alias übernimmt
    /// <see cref="VisitInitNodeAliasSymbol"/>.
    /// </summary>
    public override TagSpan<GoToTag> VisitInitNodeSymbol(IInitNodeSymbol initNodeSymbol) {
        if(initNodeSymbol.Alias != null) {
            return DefaultVisit(initNodeSymbol);
        }

        var codeModel = TaskInitCodeInfo.FromInitNode(initNodeSymbol);
        var provider  = new TaskBeginDeclarationLocationInfoProvider(_textBuffer, codeModel);

        return CreateTagSpan(initNodeSymbol.Location, provider);
    }

    /// <summary>Sprung von einem Init-Alias in die generierte <c>Begin…</c>-Deklaration im C#-Code seines Init-Knotens.</summary>
    public override TagSpan<GoToTag> VisitInitNodeAliasSymbol(IInitNodeAliasSymbol initNodeAliasSymbol) {
        var codeModel = TaskInitCodeInfo.FromInitNode(initNodeAliasSymbol.InitNode);
        var provider  = new TaskBeginDeclarationLocationInfoProvider(_textBuffer, codeModel);

        return CreateTagSpan(initNodeAliasSymbol.Location, provider);
    }

    /// <summary>Sprung von einem Signal-Trigger zur generierten Trigger-Deklaration im C#-Code (über <see cref="TriggerDeclarationLocationInfoProvider"/>).</summary>
    public override TagSpan<GoToTag> VisitSignalTriggerSymbol(ISignalTriggerSymbol signalTriggerSymbol) {

        var codeModel = SignalTriggerCodeInfo.FromSignalTrigger(signalTriggerSymbol);
        var provider  = new TriggerDeclarationLocationInfoProvider(_textBuffer, codeModel);

        return CreateTagSpan(signalTriggerSymbol.Location, provider);
    }

    /// <summary>
    /// Sprung von einem Choice-Knoten zur generierten <c>{Choice}Logic</c>-Deklaration im C#-Code (über
    /// <see cref="ChoiceLogicDeclarationLocationInfoProvider"/>) — <b>nur ab <c>#version 2</c></b>. Unter
    /// Version 1 (dem Default) wird die Choice beim Codegen platt-gefaltet und hätte kein Sprungziel; dort
    /// bietet der Besuch daher keines an.
    /// </summary>
    public override TagSpan<GoToTag> VisitChoiceNodeSymbol(IChoiceNodeSymbol choiceNodeSymbol) {

        // Erst ab Version 2 erhält eine Choice eine eigene {Choice}Logic; unter #version 1 wird sie beim
        // Codegen platt-gefaltet und hätte gar kein Sprungziel. Ohne diese Weiche böte der GoTo — da
        // Version 1 der Default ist — in der Praxis überwiegend einen Sprung an, der stets ins Leere liefe.
        var version = choiceNodeSymbol.ContainingTask.CodeGenerationUnit?.LanguageVersion ?? NavLanguageVersion.Default;
        if (version < NavLanguageVersion.Version2) {
            return DefaultVisit(choiceNodeSymbol);
        }

        var codeModel = ChoiceCodeInfo.FromChoiceNode(choiceNodeSymbol);
        var provider  = new ChoiceLogicDeclarationLocationInfoProvider(_textBuffer, codeModel);

        return CreateTagSpan(choiceNodeSymbol.Location, provider);
    }

    // Nav→Nav-Sprungziel aus dem geteilten Engine-Kern (NavGoToService/GoToTargetResolver). Für die hier
    // behandelten Symbole liefert der Kern höchstens ein Ziel; null bedeutet "kein Nav→Nav-Sprung".
    /// <summary>
    /// Liefert das Nav→Nav-Sprungziel eines Symbols aus dem geteilten Engine-Kern
    /// (<see cref="Pharmatechnik.Nav.Language.GoTo.NavGoToService"/>); für die hier behandelten Symbole
    /// höchstens eines, <c>null</c> bedeutet „kein Nav→Nav-Sprung".
    /// </summary>
    static Location ResolveNavTarget(ISymbol symbol) {
        return NavGoToService.GetGoToLocations(symbol).FirstOrDefault();
    }

    /// <summary>Baut ein Tag-Span, dessen einziges Sprungziel eine feste <see cref="LocationInfo"/> ist (über <see cref="SimpleLocationInfoProvider"/>).</summary>
    TagSpan<GoToTag> CreateGoToLocationTagSpan(Location sourceLocation, LocationInfo targetLocation) {

        var provider = new SimpleLocationInfoProvider(targetLocation);

        return CreateTagSpan(sourceLocation, provider);
    }
        
    /// <summary>
    /// Baut aus der Quell-<see cref="Location"/> im Nav-Code (dem markierten Bereich) und einem
    /// <see cref="ILocationInfoProvider"/> (der die Sprungziele beschafft) das <see cref="TagSpan{T}"/> mit
    /// dem <see cref="GoToTag"/>.
    /// </summary>
    TagSpan<GoToTag> CreateTagSpan(Location sourceLocation, ILocationInfoProvider provider) {
        var tagSpan = new SnapshotSpan(_codeGenerationUnitAndSnapshot.Snapshot, sourceLocation.Start, sourceLocation.Length);
        var tag     = new GoToTag(provider);

        return new TagSpan<GoToTag>(tagSpan, tag);
    }
}
