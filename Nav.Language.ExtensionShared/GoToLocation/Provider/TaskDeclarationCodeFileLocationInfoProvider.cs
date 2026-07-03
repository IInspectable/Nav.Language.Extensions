#region Using Directives

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Utilities.IO;
using Pharmatechnik.Nav.Language.CodeGen;
using Pharmatechnik.Nav.Language.Extension.Images;
using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider; 

class TaskDeclarationCodeFileLocationInfoProvider : CodeAnalysisLocationInfoProvider {

    readonly TaskCodeInfo _taskCodeInfo;

    public TaskDeclarationCodeFileLocationInfoProvider(ITextBuffer sourceBuffer, TaskCodeInfo taskCodeInfo): base(sourceBuffer) {
        _taskCodeInfo = taskCodeInfo;
    }

    static ImageMoniker ImageMoniker { get { return ImageMonikers.CSharpFile; } }

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(Project project, CancellationToken cancellationToken) {

        try {
            var locations = await LocationFinder.FindTaskDeclarationLocationsAsync(
                project          : project,
                codegenInfo      : _taskCodeInfo,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return locations.Select(location =>
                                        LocationInfo.FromLocation(
                                            location    : new Location(location.FilePath), // Wir sind nur an dem Dateinamen interessiert
                                            displayName : $"{PathHelper.GetRelativePath(project.FilePath, location.FilePath)}",
                                            imageMoniker: ImageMoniker))
                            .OrderBy(li => li.DisplayName);

        }
        catch (LocationNotFoundException ex) {
            return ToEnumerable(LocationInfo.FromError(ex, ImageMoniker));
        }
    }
}