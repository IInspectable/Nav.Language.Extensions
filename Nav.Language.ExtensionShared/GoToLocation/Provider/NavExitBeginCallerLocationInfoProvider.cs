#region Using Directives

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.CodeGen;
using Pharmatechnik.Nav.Language.Extension.Images;
using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider;

/// <summary>
/// Liefert für eine After-Methode (NavExit) die C#-Aufrufstellen der zugehörigen <c>BeginXY</c>-Methode.
/// Die Suche erfasst die gesamte WFS-Klasse, also auch <c>partial</c>-Deklarationen in anderen Dateien.
/// </summary>
class NavExitBeginCallerLocationInfoProvider: CodeAnalysisLocationInfoProvider {

    readonly NavExitAnnotation _exitAnnotation;

    public NavExitBeginCallerLocationInfoProvider(ITextBuffer sourceBuffer,
                                                  NavExitAnnotation exitAnnotation): base(sourceBuffer) {
        _exitAnnotation = exitAnnotation;
    }

    static ImageMoniker ImageMoniker => ImageMonikers.GoToNodeDeclaration;

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(Project project, CancellationToken cancellationToken) {

        var classSymbol = await FindContainingClassSymbolAsync(
            _exitAnnotation.ClassDeclarationSyntax.Identifier.ValueText, cancellationToken).ConfigureAwait(false);
        if (classSymbol == null) {
            return System.Array.Empty<LocationInfo>();
        }

        var beginPrefix = CodeGenFacts.BeginMethodPrefix;

        var callers = await LocationFinder.FindCallerLocations(
            project,
            classSymbol,
            call => call is NavInitCallAnnotation                       &&
                    call.TaskName    == _exitAnnotation.TaskName        &&
                    call.NavFileName == _exitAnnotation.NavFileName     &&
                    StripBeginPrefix(call.Identifier.Identifier.Text, beginPrefix) == _exitAnnotation.ExitTaskName,
            cancellationToken).ConfigureAwait(false);

        return callers.Select(caller =>
                                  LocationInfo.FromLocation(
                                      location    : caller,
                                      displayName : $"{caller.CallerName} (Zeile {caller.StartLine + 1})",
                                      imageMoniker: ImageMoniker));
    }

    static string StripBeginPrefix(string identifier, string beginPrefix) {
        return identifier.StartsWith(beginPrefix) ? identifier.Substring(beginPrefix.Length) : identifier;
    }
}
