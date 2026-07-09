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

abstract class CodeAnalysisLocationInfoProvider: LocationInfoProvider {
    readonly ITextBuffer _sourceBuffer;

    protected CodeAnalysisLocationInfoProvider(ITextBuffer sourceBuffer) {
        _sourceBuffer = sourceBuffer;
    }

    public ITextBuffer SourceBuffer => _sourceBuffer;

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