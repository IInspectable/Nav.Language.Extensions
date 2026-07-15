#region Using Directives

using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.Extension.Images;
using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider;

/// <summary>
/// Liefert für eine Init-Aufrufstelle (<c>next.Begin{Node}(…)</c>, <c>NavInitCall</c>) im generierten
/// C#-Code zwei Sprungziele: die aufgerufene <c>BeginLogic</c>-Deklaration des Ziel-Tasks und — sofern
/// zuordenbar — die zugehörige <c>After{Node}</c>-Rücksprungmethode an der Aufrufstelle. Die Zuordnung
/// (Begin-Präfix abstreifen, passende <c>NavExit</c>-Annotation finden) erledigt die VS-freie
/// Roslyn-Brücke.
/// </summary>
class NavInitCallLocationInfoProvider: CodeAnalysisLocationInfoProvider {

    readonly NavInitCallAnnotation          _callAnnotation;
    readonly IEnumerable<NavExitAnnotation> _exitAnnotations;

    /// <summary>
    /// Bindet den Provider an <paramref name="sourceBuffer"/>, die Init-Aufrufstellen-Annotation
    /// <paramref name="callAnnotation"/> und die <paramref name="exitAnnotations"/> derselben Datei, aus
    /// denen die passende After-Methode ermittelt wird.
    /// </summary>
    public NavInitCallLocationInfoProvider(ITextBuffer sourceBuffer,
                                           NavInitCallAnnotation callAnnotation,
                                           IEnumerable<NavExitAnnotation> exitAnnotations): base(sourceBuffer) {
        _callAnnotation  = callAnnotation;
        _exitAnnotations = exitAnnotations;
    }

    static ImageMoniker ImageMoniker => ImageMonikers.GoToMethodPublic;

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(Project project, CancellationToken cancellationToken) {

        LocationInfo beginLocationInfo;
        try {
            var location = await LocationFinder.FindCallBeginLogicDeclarationLocationsAsync(
                project           : project,
                initCallAnnotation: _callAnnotation,
                cancellationToken : cancellationToken).ConfigureAwait(false);

            beginLocationInfo = LocationInfo.FromLocation(
                location          : location,
                displayName       : "BeginLogic",
                imageMoniker      : ImageMoniker);

        } catch (LocationNotFoundException ex) {
            beginLocationInfo = LocationInfo.FromError(ex, ImageMoniker);
        }

        // Zweites Ziel: die zugehörige After{Node}-Rücksprungmethode. Die Zuordnung (Begin-Prefix
        // abstreifen, passende <NavExit>-Annotation suchen) macht der LocationFinder VS-frei; fehlt sie,
        // bleibt es beim BeginLogic-Ziel.
        var afterLocation = LocationFinder.FindInitCallAfterLocation(_callAnnotation, _exitAnnotations);
        if (afterLocation == null) {
            return ToEnumerable(beginLocationInfo);
        }

        var afterLocationInfo = LocationInfo.FromLocation(
            location    : afterLocation,
            displayName : afterLocation.CallerName,
            imageMoniker: ImageMoniker);

        return new[] {beginLocationInfo, afterLocationInfo};

    }

}