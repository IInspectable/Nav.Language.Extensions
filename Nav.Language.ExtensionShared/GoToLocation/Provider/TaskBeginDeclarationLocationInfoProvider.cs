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

/// <summary>
/// Springt von einem Nav-Init zur generierten <c>BeginLogic</c>-Methode der WFS-Klasse im C#-Code (Richtung
/// Nav→C#). Aus dem <see cref="TaskInitCodeInfo"/> löst die Roslyn-Brücke die Methoden-Deklaration auf;
/// angezeigt wird <c>{WfsTypeName}.{BeginLogicMethodName}</c>.
/// </summary>
class TaskBeginDeclarationLocationInfoProvider : CodeAnalysisLocationInfoProvider {

    readonly TaskInitCodeInfo _taskInitCodeInfo;

    /// <summary>Bindet den Provider an <paramref name="sourceBuffer"/> und die Codegen-Info <paramref name="taskInitCodeInfo"/> des Init.</summary>
    public TaskBeginDeclarationLocationInfoProvider(ITextBuffer sourceBuffer, TaskInitCodeInfo taskInitCodeInfo): base(sourceBuffer) {
        _taskInitCodeInfo = taskInitCodeInfo;
    }

    static ImageMoniker ImageMoniker { get { return ImageMonikers.GoToMethodPublic; } }

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(Project project, CancellationToken cancellationToken) {

        try {
            var location = await LocationFinder.FindTaskBeginDeclarationLocationAsync(
                project          : project, 
                codegenInfo      : _taskInitCodeInfo, 
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var locationInfo = LocationInfo.FromLocation(
                location    : location,
                displayName : $"{_taskInitCodeInfo.ContainingTask.WfsTypeName}.{_taskInitCodeInfo.BeginLogicMethodName}",
                imageMoniker: ImageMoniker);

            return ToEnumerable(locationInfo);

        } catch(LocationNotFoundException ex) {
            return ToEnumerable(LocationInfo.FromError(ex, ImageMoniker));
        }            
    }
}