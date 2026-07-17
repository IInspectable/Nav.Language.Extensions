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

/// <summary>
/// Springt von einer Nav-Task-Deklaration zur generierten WFS-Klasse im C#-Code (Richtung Nav→C#). Aus dem
/// <see cref="TaskCodeInfo"/> löst die Roslyn-Brücke die — ggf. mehreren partiellen — Klassen-Deklarationen
/// auf; angezeigt wird der zum Projekt relative Dateipfad, nach Anzeigename sortiert.
/// </summary>
class TaskDeclarationLocationInfoProvider: CodeAnalysisLocationInfoProvider {

    readonly TaskCodeInfo _taskCodeInfo;

    /// <summary>Bindet den Provider an <paramref name="sourceBuffer"/> und die Codegen-Info <paramref name="taskCodeInfo"/> der Task.</summary>
    public TaskDeclarationLocationInfoProvider(ITextBuffer sourceBuffer, TaskCodeInfo taskCodeInfo): base(sourceBuffer) {
        _taskCodeInfo = taskCodeInfo;
    }

    static ImageMoniker ImageMoniker { get { return ImageMonikers.GoToClassPublic; } }

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(Project project, CancellationToken cancellationToken) {

        try {
            var locations = await LocationFinder.FindTaskDeclarationLocationsAsync(
                project          : project, 
                codegenInfo      : _taskCodeInfo, 
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return locations.Select(location =>
                                        LocationInfo.FromLocation(
                                            location    : location,
                                            displayName : $"{PathHelper.GetRelativePath(project.FilePath, location.FilePath)}",
                                            imageMoniker: ImageMoniker))
                            .OrderBy(li=>li.DisplayName);                

        } catch(LocationNotFoundException ex) {
            return ToEnumerable(LocationInfo.FromError(ex, ImageMoniker));
        }           
    }
}