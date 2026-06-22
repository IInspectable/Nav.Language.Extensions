#region Using Directives

using System;
using System.Linq;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Controls.Primitives;

using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Utilities.Logging;
using Pharmatechnik.Nav.Language.Extension.UI;
using Pharmatechnik.Nav.Language.Extension.Utilities;
using Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider;

using Task = System.Threading.Tasks.Task;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation; 

[Export]
sealed class GoToLocationService {

    static readonly Logger Logger = Logger.Create<GoToLocationService>();

    const string MessageTitle             = "Nav Language Extensions";
    const string SearchingLocationMessage = "Searching Location...";
    const string OpeningFileMessage       = "Opening file...";
    const string ContextMenuHeader        = "Go To...";

    readonly IWaitIndicator _waitIndicator;

    [ImportingConstructor]
    public GoToLocationService(IWaitIndicator waitIndicator) {
        _waitIndicator = waitIndicator;
    }

    public async Task GoToLocationInPreviewTabAsync(IWpfTextView originatingTextView, Rect placementRectangle, IEnumerable<ILocationInfoProvider> provider) {
            
        List<LocationInfo> locationInfos;
        using (var waitContext = _waitIndicator.StartWait(title: MessageTitle, message: SearchingLocationMessage, allowCancel: true)) {

            try {

                var locs = await GetLocationInfosAsync(provider, waitContext.CancellationToken);
                locationInfos = locs.ToList();

                Logger.Info($"{nameof(GoToLocationInPreviewTabAsync)}: {locationInfos.Count} location(s) resolved. " +
                            String.Join(" | ", locationInfos.Select((l, i) => $"[{i}] IsValid={l.IsValid}, Display='{l.DisplayName}', Error='{l.ErrorMessage}'")));

                // Es gibt nur eine einzige Location => direkt anspringen, da wir denselben Wait Indicator verwenden wollen.
                if (locationInfos.Count == 1 && locationInfos[0].IsValid) {

                    var locationResult = locationInfos.First();

                    waitContext.AllowCancel = false;
                    waitContext.Message     = OpeningFileMessage;

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(waitContext.CancellationToken); 

                    NavLanguagePackage.GoToLocationInPreviewTab(locationResult.Location);

                    return;
                }
            } catch (OperationCanceledException) {
                return;
            }
        }

        if (locationInfos.Count == 0) {
            Logger.Info($"{nameof(GoToLocationInPreviewTabAsync)}: Keine Locations => es passiert nichts.");
            return;
        }

        // Es gibt nur eine Location, die aber nicht aufgelöst werden konnte => Fehler anzeigen und tschüss
        if (locationInfos.Count == 1 && !locationInfos[0].IsValid) {
            Logger.Info($"{nameof(GoToLocationInPreviewTabAsync)}: Einzelne, ungültige Location => Fehlermeldung: '{locationInfos[0].ErrorMessage}'");
            ShowLocationErrorMessage(locationInfos[0]);
            return;
        }

        // Wenn wir hier sind, dann gibt es mehrere Locations, für die wir eine Auswahl anzeigen müssen
        try {
            Logger.Info($"{nameof(GoToLocationInPreviewTabAsync)}: Baue Auswahlmenü (VsContextMenu) für {locationInfos.Count} Locations auf.");

            var ctxMenu = new VsContextMenu {
                Header             = ContextMenuHeader,
                PlacementTarget    = originatingTextView.VisualElement,
                PlacementRectangle = placementRectangle,
                Placement          = PlacementMode.Bottom,
                StaysOpen          = false,
                IsOpen             = true
            };

            foreach (var locationInfo in locationInfos) {

                var item = new VsMenuItem {
                    Header    = locationInfo.IsValid? locationInfo.DisplayName:locationInfo.ErrorMessage,
                    IsEnabled = locationInfo.IsValid,
                    Icon = new CrispImage {
                        Moniker   = locationInfo.ImageMoniker,
                        Grayscale = !locationInfo.IsValid
                    },
                    //InputGestureText = "<XTPlus.OffenePosten>"
                };
                item.Click += (_, _) => GoToLocationInPreviewTab(locationInfo);

                ctxMenu.Items.Add(item);
            }

            Logger.Info($"{nameof(GoToLocationInPreviewTabAsync)}: Auswahlmenü aufgebaut, IsOpen={ctxMenu.IsOpen}, Items={ctxMenu.Items.Count}.");
        } catch (Exception ex) {
            Logger.Error(ex, $"{nameof(GoToLocationInPreviewTabAsync)}: Fehler beim Aufbau des Auswahlmenüs.");
            throw;
        }
    }

    static async Task<IEnumerable<LocationInfo>> GetLocationInfosAsync(IEnumerable<ILocationInfoProvider> providers, CancellationToken cancellationToken = default) {
        using(Logger.LogBlock(nameof(GetLocationInfosAsync))) {

            var results = await Task.WhenAll(providers.Select(p => p.GetLocationsAsync(cancellationToken)));

            return results.SelectMany(x => x);
        }
    }

    void GoToLocationInPreviewTab(LocationInfo locationInfo) {
            
        ThreadHelper.ThrowIfNotOnUIThread();

        if(!locationInfo.IsValid) {
            ShowLocationErrorMessage(locationInfo);
            return;
        }

        using(_waitIndicator.StartWait(title: MessageTitle, message: OpeningFileMessage, allowCancel: false)) {
            NavLanguagePackage.GoToLocationInPreviewTab(locationInfo.Location);
        }
    }

    void ShowLocationErrorMessage(LocationInfo locationInfo) {
        ThreadHelper.ThrowIfNotOnUIThread();
        ShellUtil.ShowErrorMessage(locationInfo.ErrorMessage);
    }
}