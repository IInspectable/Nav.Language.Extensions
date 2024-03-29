﻿#region Using Directives

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

class TriggerDeclarationLocationInfoProvider: CodeAnalysisLocationInfoProvider {

    readonly SignalTriggerCodeInfo _signalTriggerCodeInfo;

    public TriggerDeclarationLocationInfoProvider(ITextBuffer sourceBuffer, SignalTriggerCodeInfo signalTriggerCodeInfo): base(sourceBuffer) {
        _signalTriggerCodeInfo = signalTriggerCodeInfo;
    }

    static ImageMoniker ImageMoniker { get { return ImageMonikers.GoToMethodPublic; } }

    protected override async Task<IEnumerable<LocationInfo>> GetLocationsAsync(Project project, CancellationToken cancellationToken) {

        try {
            var location = await LocationFinder.FindTriggerDeclarationLocationsAsync(
                project          : project, 
                codegenInfo      : _signalTriggerCodeInfo, 
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var locationInfo = LocationInfo.FromLocation(
                location    : location,
                displayName : "Go To Trigger Declaration",
                imageMoniker: ImageMoniker);

            return ToEnumerable(locationInfo);

        } catch(LocationNotFoundException ex) {
            return ToEnumerable(LocationInfo.FromError(ex, ImageMoniker));
        }
    }
}