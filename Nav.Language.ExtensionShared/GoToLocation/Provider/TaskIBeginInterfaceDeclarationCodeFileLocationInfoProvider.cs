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
using Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider; 

class TaskIBeginInterfaceDeclarationCodeFileLocationInfoProvider : CodeAnalysisLocationInfoProvider {

    readonly TaskDeclarationCodeInfo _taskDeclarationCodeInfo;

    public TaskIBeginInterfaceDeclarationCodeFileLocationInfoProvider(ITextBuffer sourceBuffer, TaskDeclarationCodeInfo taskDeclarationCodeInfo) : base(sourceBuffer) {
        _taskDeclarationCodeInfo = taskDeclarationCodeInfo;
    }

    static ImageMoniker ImageMoniker {
        get { return ImageMonikers.CSharpFile; }
    }

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(Project project, CancellationToken cancellationToken) {

        try {

            var locations = await LocationFinder.FindTaskIBeginInterfaceDeclarationLocations(
                                                     project          : project,
                                                     codegenInfo      : _taskDeclarationCodeInfo,
                                                     cancellationToken: cancellationToken)
                                                .ConfigureAwait(false);

            return locations.Select(location =>
                                        LocationInfo.FromLocation(
                                            location    : new Location(location.FilePath), // Wir sind nur an dem Dateinamen interessiert
                                            displayName : _taskDeclarationCodeInfo.FullyQualifiedBeginInterfaceName,
                                            imageMoniker: ImageMoniker))
                            .OrderBy(li => li.DisplayName);

        } catch (LocationNotFoundException ex) {
            return ToEnumerable(LocationInfo.FromError(ex, ImageMoniker));
        }
    }
}