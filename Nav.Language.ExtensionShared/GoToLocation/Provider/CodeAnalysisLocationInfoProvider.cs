#region Using Directives

using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
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
}