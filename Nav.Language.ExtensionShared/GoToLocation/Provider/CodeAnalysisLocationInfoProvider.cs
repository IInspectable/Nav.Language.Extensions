#region Using Directives

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;

using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider; 

/// <summary>
/// Basis der Provider, die ein Sprungziel im generierten C#-Code über die Roslyn-Brücke auflösen — also die
/// zum Ausgangs-Buffer gehörende Roslyn-<c>Project</c>-/Compilation-Sicht benötigen. Sie ermittelt (auf dem
/// UI-Thread) das umgebende Projekt des <see cref="SourceBuffer"/> und delegiert an die abgeleitete,
/// projektbezogene Auflösung; fehlt das Projekt (etwa bei „externen" Dokumenten), wird ein Fehler-Sprungziel
/// geliefert.
/// </summary>
abstract class CodeAnalysisLocationInfoProvider: LocationInfoProvider {
    readonly ITextBuffer _sourceBuffer;

    /// <summary>Bindet den Provider an den <paramref name="sourceBuffer"/>, dessen Projektkontext genutzt wird.</summary>
    protected CodeAnalysisLocationInfoProvider(ITextBuffer sourceBuffer) {
        _sourceBuffer = sourceBuffer;
    }

    /// <summary>Der Ausgangs-Buffer, aus dessen Projektkontext die C#-Location aufgelöst wird.</summary>
    public ITextBuffer SourceBuffer => _sourceBuffer;

    /// <summary>
    /// Wechselt auf den UI-Thread, bestimmt das umgebende Roslyn-<c>Project</c> des <see cref="SourceBuffer"/>
    /// und delegiert an <see cref="GetLocationsAsync(Project, CancellationToken)"/>. Ohne umgebendes Projekt
    /// wird ein einzelnes Fehler-Sprungziel zurückgegeben.
    /// </summary>
    public sealed override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(CancellationToken cancellationToken = new()) {
        // GetContainingProject muss auf dem Main Thread aufgerufen werden (siehe Dispatcher.VerifyAccess).
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var project = _sourceBuffer.GetContainingProject();
        if (project == null) {
            // Das kommt vor, wenn das Dokument "extern" ist, also nicht in einem der geöffneten Projekte hängt.
            // TODO Fehlermeldung überarbeiten.
            return ToEnumerable(LocationInfo.FromError("Unable to determine containing project."));
        }

        return await GetLocationsAsync(project, cancellationToken);
    }

    /// <summary>
    /// Löst die Sprungziele im generierten C#-Code des <paramref name="project"/> auf. Die Ableitung wählt
    /// je nach Nav-Ausgangssymbol den passenden
    /// <see cref="Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols.LocationFinder"/>-Aufruf.
    /// </summary>
    protected abstract Task<IEnumerable<LocationInfo>> GetLocationsAsync(Project project, CancellationToken cancellationToken);

    /// <summary>
    /// Löst die (ggf. partielle) Klasse mit dem angegebenen einfachen Namen aus dem aktuellen Buffer-Snapshot
    /// zu einem <see cref="INamedTypeSymbol"/> auf — dem Eingang für die VS-freie Aufrufer-Suche
    /// <see cref="Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols.LocationFinder.FindCallerLocations"/>.
    /// Bewusst über das aktuelle Dokument (nicht die evtl. veraltete Annotations-Syntax), damit das Symbol
    /// alle aktuellen partiellen Deklarationen kennt. Liefert <c>null</c>, wenn kein Dokument/Modell/Klasse
    /// vorliegt.
    /// </summary>
    protected async Task<INamedTypeSymbol> FindContainingClassSymbolAsync(string className, CancellationToken cancellationToken) {

        var document = SourceBuffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges();
        if (document == null) {
            return null;
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel == null) {
            return null;
        }

        var root = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);

        var classDeclaration = root.DescendantNodesAndSelf()
                                   .OfType<ClassDeclarationSyntax>()
                                   .FirstOrDefault(c => c.Identifier.ValueText == className);
        if (classDeclaration == null) {
            return null;
        }

        return semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) as INamedTypeSymbol;
    }
}