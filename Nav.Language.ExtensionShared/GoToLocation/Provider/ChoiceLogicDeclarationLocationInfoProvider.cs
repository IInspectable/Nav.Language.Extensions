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

class ChoiceLogicDeclarationLocationInfoProvider : CodeAnalysisLocationInfoProvider {

    readonly ChoiceCodeInfo _choiceCodeInfo;

    public ChoiceLogicDeclarationLocationInfoProvider(ITextBuffer sourceBuffer, ChoiceCodeInfo choiceCodeInfo): base(sourceBuffer) {
        _choiceCodeInfo = choiceCodeInfo;
    }

    static ImageMoniker ImageMoniker { get { return ImageMonikers.GoToMethodPublic; } }

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(Project project, CancellationToken cancellationToken) {

        try {

            var location = await LocationFinder.FindChoiceLogicDeclarationLocationAsync(
                project          : project,
                codegenInfo      : _choiceCodeInfo,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var locationInfo = LocationInfo.FromLocation(
                location    : location,
                displayName : $"{_choiceCodeInfo.ContainingTask.WfsTypeName}.{_choiceCodeInfo.ChoiceLogicMethodName}",
                imageMoniker: ImageMoniker);

            return ToEnumerable(locationInfo);

        } catch(LocationNotFoundException ex) {
            return ToEnumerable(LocationInfo.FromError(ex, ImageMoniker));
        }
    }
}
