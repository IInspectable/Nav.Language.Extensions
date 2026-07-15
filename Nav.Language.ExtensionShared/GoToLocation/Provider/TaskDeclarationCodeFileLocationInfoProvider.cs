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
/// Variante von <see cref="TaskDeclarationLocationInfoProvider"/>, die nicht zur konkreten
/// Klassen-Deklaration, sondern nur zur enthaltenden C#-Datei der generierten WFS-Klasse springt (die
/// Location wird auf den reinen Dateipfad reduziert). Angezeigt wird der zum Projekt relative Pfad mit dem
/// CSharpFile-Icon.
/// </summary>
class TaskDeclarationCodeFileLocationInfoProvider : CodeAnalysisLocationInfoProvider {

    readonly TaskCodeInfo _taskCodeInfo;

    /// <summary>Bindet den Provider an <paramref name="sourceBuffer"/> und die Codegen-Info <paramref name="taskCodeInfo"/> der Task.</summary>
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