#region Using Directives

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

class TaskExitDeclarationLocationInfoProvider : CodeAnalysisLocationInfoProvider {

    readonly TaskExitCodeInfo _taskExitCodeInfo;

    public TaskExitDeclarationLocationInfoProvider(ITextBuffer sourceBuffer, TaskExitCodeInfo taskExitCodeInfo): base(sourceBuffer) {
        _taskExitCodeInfo = taskExitCodeInfo;
    }

    static ImageMoniker ImageMoniker { get { return ImageMonikers.GoToMethodPublic; } }

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(Project project, CancellationToken cancellationToken) {

        try {

            var location = await LocationFinder.FindTaskExitDeclarationLocationAsync(
                project          : project, 
                codegenInfo      : _taskExitCodeInfo, 
                cancellationToken: cancellationToken).ConfigureAwait(false);
                
            var locationInfo = LocationInfo.FromLocation(
                location    : location,
                displayName : $"{_taskExitCodeInfo.ContainingTask.WfsTypeName}.{_taskExitCodeInfo.AfterLogicMethodName}",
                imageMoniker: ImageMoniker);

            return ToEnumerable(locationInfo);

        } catch(LocationNotFoundException ex) {
            return ToEnumerable(LocationInfo.FromError(ex, ImageMoniker));
        }
    }
}