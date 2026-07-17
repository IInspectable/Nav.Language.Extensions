#region Using Directives

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
using Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider;
using Pharmatechnik.Nav.Language.Extension.Images;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CSharp.GoTo; 

/// <summary>
/// Übersetzt die Nav-Annotationen einer generierten C#-Datei in <see cref="IntraTextGoToTag"/>-
/// Tag-Spans: Je Annotation (Task, Init, Exit, Trigger, Choice sowie deren Aufrufstellen) bestimmt der
/// Visitor die zu markierende Bezeichner-Position im <see cref="ITextSnapshot"/> und den zugehörigen
/// <see cref="Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider.ILocationInfoProvider"/>, der
/// beim Klick das/die Nav-Sprungziel(e) auflöst. Als
/// <see cref="NavTaskAnnotationVisitor{TResult}"/> liefert jede <c>Visit…</c>-Methode das Tag-Span oder
/// <c>null</c>, wenn keine Markierung entsteht.
/// </summary>
class IntraTextGoToTagSpanBuilder: NavTaskAnnotationVisitor<ITagSpan<IntraTextGoToTag>> {

    const string ToolTipGoToTaskDefinition    = "Go To Task Definition";
    const string ToolTipGoToInitDefinition    = "Go To Init Definition";
    const string ToolTipGoToExitDefinition    = "Go To Exit Definition";
    const string ToolTipGoToTriggerDefinition = "Go To Trigger Definition";
    const string ToolTipGoToChoiceDefinition  = "Go To Choice Definition";
    const string ToolTipGoToImplementation    = "Go To Implementation";

    readonly ImmutableList<NavTaskAnnotation> _allAnnotations;
    readonly ITextSnapshot                    _textSnapshot;

    public IntraTextGoToTagSpanBuilder(IEnumerable<NavTaskAnnotation> allAnnotations,
                                       ITextSnapshot textSnapshot) {
        _allAnnotations = allAnnotations.ToImmutableList();
        _textSnapshot   = textSnapshot;
    }

    /// <summary>Symbol am WFS-Klassennamen → springt zur Task-Definition im <c>.nav</c>.</summary>
    public override ITagSpan<IntraTextGoToTag> VisitNavTaskAnnotation(NavTaskAnnotation navTaskAnnotation) {

        var start  = navTaskAnnotation.ClassDeclarationSyntax.Identifier.Span.Start;
        var length = navTaskAnnotation.ClassDeclarationSyntax.Identifier.Span.Length;

        var snapshotSpan = new SnapshotSpan(_textSnapshot, start, length);
        var provider     = new NavTaskAnnotationLocationInfoProvider(navTaskAnnotation);
        var tag = new IntraTextGoToTag(
            provider    : provider, 
            imageMoniker: ImageMonikers.GoToDefinition,
            toolTip     : ToolTipGoToTaskDefinition);

        return new TagSpan<IntraTextGoToTag>(snapshotSpan, tag);
    }

    /// <summary>Symbol an der Init-Methode → springt zum <c>init</c>-Verbindungspunkt im <c>.nav</c>.</summary>
    public override ITagSpan<IntraTextGoToTag> VisitNavInitAnnotation(NavInitAnnotation navInitAnnotation) {

        int start  = navInitAnnotation.MethodDeclarationSyntax.Identifier.Span.Start;
        int length = navInitAnnotation.MethodDeclarationSyntax.Identifier.Span.Length;

        var snapshotSpan = new SnapshotSpan(_textSnapshot, start, length);
        var provider     = new NavInitAnnotationLocationInfoProvider(navInitAnnotation);
        var tag = new IntraTextGoToTag(
            provider    : provider, 
            imageMoniker: ImageMonikers.GoToDefinition, 
            toolTip     : ToolTipGoToInitDefinition);

        return new TagSpan<IntraTextGoToTag>(snapshotSpan, tag);
    }

    /// <summary>
    /// Symbol an der Exit-Methode → springt zum <c>exit</c>-Verbindungspunkt im <c>.nav</c> und bietet
    /// zusätzlich die C#-Aufrufstellen der zugehörigen <c>Begin…</c>-Methode an (klassenweit, inkl.
    /// Teildeklarationen anderer Dateien).
    /// </summary>
    public override ITagSpan<IntraTextGoToTag> VisitNavExitAnnotation(NavExitAnnotation navExitAnnotation) {

        int start  = navExitAnnotation.MethodDeclarationSyntax.Identifier.Span.Start;
        int length = navExitAnnotation.MethodDeclarationSyntax.Identifier.Span.Length;

        var snapshotSpan = new SnapshotSpan(_textSnapshot, start, length);
        var provider     = new NavExitAnnotationLocationInfoProvider(navExitAnnotation);
        var tag = new IntraTextGoToTag(
            provider    : provider,
            imageMoniker: ImageMonikers.GoToDefinition,
            toolTip     : ToolTipGoToExitDefinition);

        // Zusätzlich zu den Nav-Exit-Zielen die C#-Aufrufstellen der zugehörigen BeginXY-Methode anbieten
        // (klassenweit, inkl. partial-Deklarationen in anderen Dateien).
        tag.Provider.Add(new NavExitBeginCallerLocationInfoProvider(
                             sourceBuffer  : _textSnapshot.TextBuffer,
                             exitAnnotation: navExitAnnotation));

        return new TagSpan<IntraTextGoToTag>(snapshotSpan, tag);
    }

    /// <summary>Symbol an der Trigger-Methode → springt zur Trigger-Definition im <c>.nav</c>.</summary>
    public override ITagSpan<IntraTextGoToTag> VisitNavTriggerAnnotation(NavTriggerAnnotation navTriggerAnnotation) {

        int start  = navTriggerAnnotation.MethodDeclarationSyntax.Identifier.Span.Start;
        int length = navTriggerAnnotation.MethodDeclarationSyntax.Identifier.Span.Length;

        var snapshotSpan = new SnapshotSpan(_textSnapshot, start, length);
        var provider     = new NavTriggerAnnotationLocationInfoProvider(navTriggerAnnotation);
        var tag = new IntraTextGoToTag(
            provider    : provider, 
            imageMoniker: ImageMonikers.GoToDefinition, 
            toolTip     : ToolTipGoToTriggerDefinition);

        return new TagSpan<IntraTextGoToTag>(snapshotSpan, tag);
    }

    /// <summary>
    /// Symbol an der <c>{Choice}Logic</c>-Methode → springt zum Choice-Knoten im <c>.nav</c> und bietet
    /// zusätzlich die C#-Aufrufstellen der zugehörigen <c>{Choice}(…)</c>-Forwards (<c>next.{Choice}(…)</c>)
    /// an — klassenweit, inkl. Teildeklarationen anderer Dateien.
    /// </summary>
    public override ITagSpan<IntraTextGoToTag> VisitNavChoiceAnnotation(NavChoiceAnnotation navChoiceAnnotation) {

        int start  = navChoiceAnnotation.MethodDeclarationSyntax.Identifier.Span.Start;
        int length = navChoiceAnnotation.MethodDeclarationSyntax.Identifier.Span.Length;

        var snapshotSpan = new SnapshotSpan(_textSnapshot, start, length);
        var provider     = new NavChoiceAnnotationLocationInfoProvider(navChoiceAnnotation);
        var tag = new IntraTextGoToTag(
            provider    : provider,
            imageMoniker: ImageMonikers.GoToDefinition,
            toolTip     : ToolTipGoToChoiceDefinition);

        // Zusätzlich zum Nav-Choice-Knoten die C#-Aufrufstellen der zugehörigen {Choice}(…)-Forwards
        // (next.{Choice}(…)) anbieten — klassenweit, inkl. partial-Deklarationen in anderen Dateien.
        tag.Provider.Add(new NavChoiceCallerLocationInfoProvider(
                             sourceBuffer    : _textSnapshot.TextBuffer,
                             choiceAnnotation: navChoiceAnnotation));

        return new TagSpan<IntraTextGoToTag>(snapshotSpan, tag);
    }

    /// <summary>
    /// Symbol an der Choice-Aufrufstelle (<c>next.{Choice}(…)</c>) → springt zum Choice-Knoten im
    /// <c>.nav</c> und bietet zusätzlich die C#-Implementierung der geteilten <c>{Choice}Logic</c> an
    /// (Abstieg von der <c>{Task}WFSBase</c> auf die abgeleitete Nutzer-Klasse).
    /// </summary>
    public override ITagSpan<IntraTextGoToTag> VisitNavChoiceCallAnnotation(NavChoiceCallAnnotation navChoiceCallAnnotation) {

        var start  = navChoiceCallAnnotation.Identifier.Span.Start;
        var length = navChoiceCallAnnotation.Identifier.Span.Length;

        var snapshotSpan = new SnapshotSpan(_textSnapshot, start, length);
        var provider     = new NavChoiceCallLocationInfoProvider(navChoiceCallAnnotation);
        var tag = new IntraTextGoToTag(
            provider    : provider,
            imageMoniker: ImageMonikers.GoToDefinition,
            toolTip     : ToolTipGoToChoiceDefinition);

        // Zusätzlich zum Nav-Choice-Knoten die C#-Implementierung der geteilten {Choice}Logic anbieten
        // (Abstieg von der {Task}WFSBase auf die abgeleitete Nutzer-Klasse).
        tag.Provider.Add(new NavChoiceCallLogicLocationInfoProvider(
                             sourceBuffer  : _textSnapshot.TextBuffer,
                             callAnnotation: navChoiceCallAnnotation));

        return new TagSpan<IntraTextGoToTag>(snapshotSpan, tag);
    }

    /// <summary>
    /// Symbol an der Init-Aufrufstelle (<c>next.Begin…(…)</c>) → bietet neben der <c>BeginLogic</c>-
    /// Implementierung auch die zugehörige <c>After{Node}</c>-Rücksprungmethode an; die konkrete
    /// Zuordnung übernimmt der LocationFinder, hier werden nur die Exit-Kandidaten des aktuellen
    /// Dokuments durchgereicht.
    /// </summary>
    public override ITagSpan<IntraTextGoToTag> VisitNavInitCallAnnotation(NavInitCallAnnotation navInitCallAnnotation) {

        var start  = navInitCallAnnotation.Identifier.Span.Start;
        var length = navInitCallAnnotation.Identifier.Span.Length;

        var snapshotSpan = new SnapshotSpan(_textSnapshot, start, length);

        // Neben der BeginLogic-Implementierung auch die zugehörige After{Node}-Rücksprungmethode anbieten;
        // die Zuordnung (Begin-Prefix abstreifen, passende <NavExit>-Annotation wählen) übernimmt der
        // LocationFinder — hier nur die Kandidaten aus dem aktuellen Dokument durchreichen.
        var provider = new NavInitCallLocationInfoProvider(
            sourceBuffer   : _textSnapshot.TextBuffer,
            callAnnotation : navInitCallAnnotation,
            exitAnnotations: _allAnnotations.OfType<NavExitAnnotation>());

        var tag = new IntraTextGoToTag(
            provider      : provider,
            imageMoniker  : ImageMonikers.GoToDeclaration,
            toolTip       : ToolTipGoToImplementation);

        return new TagSpan<IntraTextGoToTag>(snapshotSpan, tag);
    }
}