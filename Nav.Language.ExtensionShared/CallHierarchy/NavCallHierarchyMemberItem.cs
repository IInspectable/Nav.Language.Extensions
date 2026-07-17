#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media;

using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.CallHierarchy;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using Pharmatechnik.Nav.Language.CallHierarchy;
using Pharmatechnik.Nav.Language.Text;

using Task = System.Threading.Tasks.Task;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CallHierarchy;

/// <summary>
/// Ein Knoten der VS-Aufrufhierarchie = eine Nav-Task. Implementiert den VS-Kontrakt
/// <see cref="ICallHierarchyMemberItem"/> (Pendant zum LSP-<c>CallHierarchyItem</c>). Der Knoten trägt
/// nur <see cref="FilePath"/> + <see cref="Offset"/> (Bezeichner-Position) zur Identität; die Task wird
/// bei jeder Expansion frisch über <see cref="NavCallHierarchyService.PrepareCallHierarchy"/> aufgelöst
/// (Freshness-Muster wie LSP-<c>ResolveCallHierarchyTask</c>) — daher hält der Knoten kein Symbol fest.
/// <para>
/// Die Suche läuft pro Kategorie über <see cref="StartSearch"/> (nicht blockierend, Ergebnisse via
/// <see cref="ICallHierarchySearchCallback.AddResult(ICallHierarchyMemberItem)"/>); jede Richtung hat
/// eine eigene <see cref="CancellationTokenSource"/> (Abbruch über <see cref="CancelSearch"/>). Step 2
/// liefert die ausgehenden Aufrufe (<c>Callees</c>); die eingehenden (<c>Callers</c>) folgen in Step 3.
/// </para>
/// </summary>
sealed class NavCallHierarchyMemberItem: ICallHierarchyMemberItem {

    readonly ImageMoniker                                _glyphMoniker;
    readonly Location                                    _navigationTarget;
    readonly IReadOnlyList<ICallHierarchyItemDetails>    _details;
    readonly CallHierarchySearchCategory[]               _searchCategories;
    readonly Dictionary<string, CancellationTokenSource> _searches = new();
    readonly object                                      _gate     = new();

    public NavCallHierarchyMemberItem(string memberName,
                                      string containingTypeName,
                                      Location navigationTarget,
                                      string filePath,
                                      int offset,
                                      ImageMoniker glyphMoniker,
                                      IReadOnlyList<ICallHierarchyItemDetails> details = null) {
        MemberName         = memberName;
        ContainingTypeName = containingTypeName;
        _navigationTarget  = navigationTarget;
        FilePath           = filePath;
        Offset             = offset;
        _glyphMoniker      = glyphMoniker;
        _details           = details ?? Array.Empty<ICallHierarchyItemDetails>();

        // Jeder Knoten bietet beide Richtungen an, damit sich die Hierarchie beliebig tief in beide
        // Richtungen expandieren lässt (echte Rekursion). Eingehend ("Calls To") zuerst — wie die
        // C#-Call-Hierarchy in VS.
        _searchCategories = new[] {
            new CallHierarchySearchCategory(CallHierarchyPredefinedSearchCategoryNames.Callers, $"Calls To '{memberName}'"),
            new CallHierarchySearchCategory(CallHierarchyPredefinedSearchCategoryNames.Callees, $"Calls From '{memberName}'")
        };
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

    public IEnumerable<ICallHierarchyItemDetails>   Details                   => _details;
    public IEnumerable<CallHierarchySearchCategory> SupportedSearchCategories => _searchCategories;

    public bool SupportsNavigateTo     => true;
    public bool SupportsFindReferences => false;
    public bool Valid                  => true;

    /// <summary>Springt (im Vorschautab) zur Task-Definition dieses Knotens.</summary>
    public void NavigateTo() {
        NavLanguagePackage.Jtf.RunAsync(async () => {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            NavLanguagePackage.GoToLocationInPreviewTab(_navigationTarget);
        }).FileAndForget("nav/extension/callhierarchy/navigate");
    }

    // --- Suche -------------------------------------------------------------------------------------

    /// <summary>
    /// Startet die (nicht blockierende) Suche für eine Kategorie: pro Kategorie eine eigene
    /// <see cref="CancellationTokenSource"/> (analog Roslyns <c>CallHierarchyItem.StartSearch</c>), Arbeit
    /// auf dem Threadpool, Ergebnisse über den Callback, am Ende immer
    /// <see cref="ICallHierarchySearchCallback.SearchSucceeded"/>/<c>SearchFailed</c>.
    /// </summary>
    public void StartSearch(string categoryName, CallHierarchySearchScope searchScope, ICallHierarchySearchCallback callback) {

        CancellationTokenSource cts;
        lock (_gate) {
            CancelSearch_NoLock(categoryName);
            cts                     = new CancellationTokenSource();
            _searches[categoryName] = cts;
        }

        Task.Run(() => SearchWorkerAsync(categoryName, callback, cts.Token))
            .FileAndForget("nav/extension/callhierarchy/search");
    }

    async Task SearchWorkerAsync(string categoryName, ICallHierarchySearchCallback callback, CancellationToken cancellationToken) {

        string errorMessage = null;
        try {

            var solution = await NavLanguagePackage.GetSolutionAsync(cancellationToken).ConfigureAwait(false);
            var unit     = solution.SemanticModelProvider.GetSemanticModel(FilePath, cancellationToken);
            var task     = unit == null ? null : NavCallHierarchyService.PrepareCallHierarchy(unit, Offset);

            if (task != null) {
                if (categoryName == CallHierarchyPredefinedSearchCategoryNames.Callees) {
                    AddOutgoingCalls(task, unit, callback, cancellationToken);
                } else if (categoryName == CallHierarchyPredefinedSearchCategoryNames.Callers) {
                    await AddIncomingCallsAsync(task, solution, callback, cancellationToken).ConfigureAwait(false);
                }
            }

        } catch (OperationCanceledException) {
            errorMessage = "Abgebrochen";
        } catch (Exception ex) {
            errorMessage = ex.Message;
        } finally {
            lock (_gate) {
                _searches.Remove(categoryName);
            }
            if (errorMessage != null) {
                callback.SearchFailed(errorMessage);
            } else {
                callback.SearchSucceeded();
            }
        }
    }

    /// <summary>Ausgehende Aufrufe: je aufgerufener Task ein Kind-Knoten mit den Aufrufstellen als Details.</summary>
    static void AddOutgoingCalls(ITaskDefinitionSymbol task, CodeGenerationUnit unit, ICallHierarchySearchCallback callback, CancellationToken cancellationToken) {

        // Die Aufrufstellen liegen in der aufrufenden Task, also in DIESER Datei → Zeilentext aus deren SourceText.
        var sourceText = unit.Syntax.SyntaxTree.SourceText;

        foreach (var call in NavCallHierarchyService.GetOutgoingCalls(task)) {

            cancellationToken.ThrowIfCancellationRequested();

            var child = NavCallHierarchyItemFactory.FromDeclaration(call.Target, BuildDetails(sourceText, call.CallSites));
            if (child != null) {
                callback.AddResult(child);
            }
        }
    }

    /// <summary>Eingehende Aufrufe: je aufrufender Task ein Kind-Knoten mit den Aufrufstellen als Details.</summary>
    static async Task AddIncomingCallsAsync(ITaskDefinitionSymbol task, NavSolution solution, ICallHierarchySearchCallback callback, CancellationToken cancellationToken) {

        var calls = await NavCallHierarchyService.GetIncomingCallsAsync(task, solution, cancellationToken).ConfigureAwait(false);

        foreach (var call in calls) {

            cancellationToken.ThrowIfCancellationRequested();

            // Die Aufrufstellen liegen in der AUFRUFENDEN Task (ggf. andere Datei) → Zeilentext aus deren SourceText.
            // Für einen aufgelösten Caller ist die gesamte Kette (Unit → Syntax → Tree → SourceText) non-null.
            // ReSharper disable once PossibleNullReferenceException
            var sourceText = call.Caller.CodeGenerationUnit.Syntax.SyntaxTree.SourceText;

            var child = NavCallHierarchyItemFactory.FromDefinition(call.Caller, BuildDetails(sourceText, call.CallSites));
            if (child != null) {
                callback.AddResult(child);
            }
        }
    }

    static IReadOnlyList<ICallHierarchyItemDetails> BuildDetails(SourceText sourceText, IReadOnlyList<Location> callSites) {
        return callSites.Select(callSite => CreateDetail(sourceText, callSite))
                        .ToList<ICallHierarchyItemDetails>();
    }

    static NavCallHierarchyDetail CreateDetail(SourceText sourceText, Location callSite) {
        var line = sourceText.GetTextLineAtPosition(callSite.Start);
        var text = sourceText.Substring(line.ExtentWithoutLineEndings).Trim();
        return new NavCallHierarchyDetail(callSite, text);
    }

    /// <summary>Bricht eine laufende Suche der angegebenen Kategorie ab.</summary>
    public void CancelSearch(string categoryName) {
        lock (_gate) {
            CancelSearch_NoLock(categoryName);
        }
    }

    void CancelSearch_NoLock(string categoryName) {
        if (_searches.TryGetValue(categoryName, out var cts)) {
            cts.Cancel();
        }
    }

    public void SuspendSearch(string categoryName) => CancelSearch(categoryName);
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
