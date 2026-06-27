#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;

using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.CallHierarchy;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CallHierarchy;

/// <summary>
/// Ein Knoten der VS-Aufrufhierarchie = eine Nav-Task. Implementiert den VS-Kontrakt
/// <see cref="ICallHierarchyMemberItem"/> (Pendant zum LSP-<c>CallHierarchyItem</c>). Der Knoten trägt
/// nur <see cref="FilePath"/> + <see cref="Offset"/> (Bezeichner-Position) zur Identität; die Task wird
/// bei jeder Expansion frisch über <c>NavCallHierarchyService.PrepareCallHierarchy</c> aufgelöst
/// (Freshness-Muster wie LSP-<c>ResolveCallHierarchyTask</c>) — daher hält der Knoten kein Symbol fest.
/// <para>
/// Die eigentliche Suche (ein-/ausgehende Aufrufe) folgt in Step 2/3; in Step 1 ist der Knoten die
/// reine Wurzel (keine Such-Kategorien), navigierbar per Doppelklick.
/// </para>
/// </summary>
sealed class NavCallHierarchyMemberItem: ICallHierarchyMemberItem {

    readonly ImageMoniker _glyphMoniker;
    readonly Location     _navigationTarget;

    public NavCallHierarchyMemberItem(string memberName,
                                      string containingTypeName,
                                      Location navigationTarget,
                                      string filePath,
                                      int offset,
                                      ImageMoniker glyphMoniker) {
        MemberName         = memberName;
        ContainingTypeName = containingTypeName;
        _navigationTarget  = navigationTarget;
        FilePath           = filePath;
        Offset             = offset;
        _glyphMoniker      = glyphMoniker;
    }

    /// <summary>Datei der Task-Definition (Bezeichner) — Teil der Knoten-Identität für die Neuauflösung.</summary>
    public string FilePath { get; }

    /// <summary>0-basierter Bezeichner-Offset — Anker für <c>PrepareCallHierarchy</c> bei der Expansion.</summary>
    public int Offset { get; }

    public string MemberName              { get; }
    public string ContainingTypeName      { get; }
    public string ContainingNamespaceName => string.Empty;
    public string NameSeparator           => ".";
    public string SortText                => MemberName;

    // DisplayGlyph wird vom Toolfenster (WPF) auf dem UI-Thread gebunden — daher hier (lazy) den
    // ImageMoniker in eine ImageSource wandeln, statt eager beim (ggf. Hintergrund-)Bau des Knotens.
    public ImageSource DisplayGlyph {
        get {
            ThreadHelper.ThrowIfNotOnUIThread();
            return ToImageSource(_glyphMoniker);
        }
    }

    // Wurzel hat keine eigenen Aufrufstellen; die Details der Kinder folgen in Step 2/3.
    public IEnumerable<ICallHierarchyItemDetails> Details => Enumerable.Empty<ICallHierarchyItemDetails>();

    // Such-Kategorien (Callers/Callees) kommen in Step 2/3 dazu.
    public IEnumerable<CallHierarchySearchCategory> SupportedSearchCategories => Enumerable.Empty<CallHierarchySearchCategory>();

    public bool SupportsNavigateTo     => true;
    public bool SupportsFindReferences => false;
    public bool Valid                  => true;

    public void NavigateTo() {
        NavLanguagePackage.Jtf.RunAsync(async () => {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            NavLanguagePackage.GoToLocationInPreviewTab(_navigationTarget);
        }).FileAndForget("nav/extension/callhierarchy/navigate");
    }

    // --- Suche: Platzhalter für Step 2/3 -------------------------------------------------------------
    // StartSearch darf nicht blockieren und MUSS am Ende SearchSucceeded()/SearchFailed() melden.
    // Solange keine Kategorien angeboten werden, ruft das Toolfenster StartSearch nicht auf; der
    // sofortige SearchSucceeded() ist nur defensiv.
    public void StartSearch(string categoryName, CallHierarchySearchScope searchScope, ICallHierarchySearchCallback callback) {
        callback.SearchSucceeded();
    }

    public void CancelSearch(string categoryName)  { }
    public void SuspendSearch(string categoryName) { }
    public void ResumeSearch(string categoryName)  { }
    public void ItemSelected()                     { }
    public void FindReferences()                   { }

    /// <summary>
    /// Wandelt einen <see cref="ImageMoniker"/> über den VS-Image-Service in eine WPF-
    /// <see cref="ImageSource"/> (DisplayGlyph erwartet ImageSource, keinen Moniker).
    /// </summary>
    static ImageSource ToImageSource(ImageMoniker moniker) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var imageService = NavLanguagePackage.GetGlobalService<SVsImageService, IVsImageService2>();
        if (imageService == null) {
            return null;
        }

        var attributes = new ImageAttributes {
            StructSize    = Marshal.SizeOf(typeof(ImageAttributes)),
            Dpi           = 96,
            Flags         = (uint) _ImageAttributesFlags.IAF_RequiredFlags,
            ImageType     = (uint) _UIImageType.IT_Bitmap,
            Format        = (uint) _UIDataFormat.DF_WPF,
            LogicalHeight = 16,
            LogicalWidth  = 16
        };

        object data  = null;
        var    image = imageService.GetImage(moniker, attributes);
        image?.get_Data(out data);

        return data as ImageSource;
    }

}
