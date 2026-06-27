#region Using Directives

using Microsoft.VisualStudio.Language.CallHierarchy;
using Microsoft.VisualStudio.Shell;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CallHierarchy;

/// <summary>
/// Eine einzelne Aufrufstelle eines Knotens (ein TaskNode-Bezeichner) für VS' Call-Hierarchy-Detailbereich.
/// Implementiert <see cref="ICallHierarchyItemDetails"/>; Zeilen/Spalten sind — wie von VS erwartet —
/// 0-basiert und kommen direkt aus der Engine-<see cref="Location"/>.
/// </summary>
sealed class NavCallHierarchyDetail: ICallHierarchyItemDetails {

    readonly Location _location;

    public NavCallHierarchyDetail(Location location, string text) {
        _location = location;
        Text      = text;
    }

    public string Text { get; }

    public string File        => _location.FilePath;
    public int    StartLine   => _location.StartLine;
    public int    StartColumn => _location.StartCharacter;
    public int    EndLine     => _location.EndLine;
    public int    EndColumn   => _location.EndCharacter;

    public bool SupportsNavigateTo => true;

    public void NavigateTo() {
        NavLanguagePackage.Jtf.RunAsync(async () => {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            NavLanguagePackage.GoToLocationInPreviewTab(_location);
        }).FileAndForget("nav/extension/callhierarchy/navigate-detail");
    }

}
