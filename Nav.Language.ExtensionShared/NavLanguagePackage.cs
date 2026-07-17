#region Using Directives

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using EnvDTE;

using JetBrains.Annotations;

using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.LanguageService;
using Pharmatechnik.Nav.Utilities.Logging;

using Control = System.Windows.Controls.Control;
using Project = Microsoft.CodeAnalysis.Project;
using Task = System.Threading.Tasks.Task;

#endregion

namespace Pharmatechnik.Nav.Language.Extension; 

/// <summary>
/// Das Visual-Studio-Package der Nav Language — der Einstiegspunkt der Extension. Als
/// <see cref="AsyncPackage"/> registriert es den <see cref="NavLanguageService"/> (samt Editor-Optionen
/// via <see cref="ProvideLanguageServiceAttribute"/>), wärmt beim Laden den
/// <see cref="NavSolutionProvider"/>-Cache vor und stellt eine statische Service-Locator-Fassade bereit
/// (Zugriff auf DTE, Roslyn-<see cref="VisualStudioWorkspace"/>, den aktiven <see cref="IWpfTextView"/>,
/// die Nav-<see cref="NavSolution"/> u.a.) sowie die package-weite <see cref="Jtf"/>. Über die partielle
/// Aufteilung kommen die Options-Registrierung (<c>NavLanguagePackage.AdvancedOptions.cs</c>) und die
/// GUID-Konstanten (<c>NavLanguagePackage.Guids.cs</c>) hinzu.
/// </summary>
[ProvideLanguageService(typeof(NavLanguageService),
                        NavLanguageContentDefinitions.LanguageName,
                        101,
                        AutoOutlining               = true,
                        MatchBraces                 = true,
                        ShowSmartIndent             = false,
                        DefaultToInsertSpaces       = true,
                        MatchBracesAtCaret          = true,
                        EnableAsyncCompletion       = true,
                        ShowCompletion              = true,
                        RequestStockColors          = true,
                        EnableLineNumbers           = true,
                        EnableAdvancedMembersOption = false,
                        ShowMatchingBrace           = true,
                        ShowDropDownOptions         = true)]
[InstalledProductRegistration("#110", "#112", MyAssembly.ProductVersion, IconResourceID = 400)]
[ProvideLanguageExtension(typeof(NavLanguageService), NavLanguageContentDefinitions.FileExtension)]
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(Guids.PackageGuidString)]
[ProvideShowBraceCompletion]
[ProvideShowDropdownBarOption]
[ProvideService(typeof(NavLanguageService), IsAsyncQueryable = true)]
[ProvideService(typeof(NavLanguagePackage), IsAsyncQueryable = true)]
sealed partial class NavLanguagePackage: AsyncPackage {

    static readonly Logger Logger = Logger.Create<NavLanguagePackage>();

    /// <summary>
    /// Erzeugt das Package und initialisiert das Logging in das Temp-Verzeichnis.
    /// </summary>
    public NavLanguagePackage() {
        LoggerConfig.Initialize(Path.GetTempPath(), "Nav.Language.Extension");
    }

    static JoinableTaskFactory _jtf;

    /// <summary>
    /// Die <see cref="JoinableTaskFactory"/> dieses Package. Im Gegensatz zu
    /// <c>ThreadHelper.JoinableTaskFactory</c> werden hierüber gestartete Tasks beim
    /// Herunterfahren der IDE sauber abgewartet bzw. abgebrochen (siehe VSSDK007). Wird für
    /// Fire-and-forget-Aufrufe (RunAsync) verwendet. Solange das Package noch nicht geladen ist,
    /// wird auf <c>ThreadHelper.JoinableTaskFactory</c> zurückgegriffen.
    /// </summary>
    internal static JoinableTaskFactory Jtf => _jtf ?? ThreadHelper.JoinableTaskFactory;

    /// <summary>
    /// Initialisiert das Package: hinterlegt die <see cref="Jtf"/>, registriert
    /// <see cref="NavLanguageService"/> und Package als asynchron abfragbare Dienste und wärmt den
    /// <see cref="NavSolutionProvider"/>-Cache vor.
    /// </summary>
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress) {

        _jtf = JoinableTaskFactory;

        await base.InitializeAsync(cancellationToken, progress).ConfigureAwait(false);

        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        #pragma warning disable VSSDK006 // Check services exist
        var shell    = (IVsShell)    await GetServiceAsync(typeof(SVsShell)).ConfigureAwait(true);
        var solution = (IVsSolution) await GetServiceAsync(typeof(SVsSolution)).ConfigureAwait(true);
        #pragma warning restore VSSDK006 // Check services exist

        cancellationToken.ThrowIfCancellationRequested();
        Assumes.Present(shell);
        Assumes.Present(solution);

        AddService(typeof(NavLanguageService), async (_, ct, _)
                       => {
                       await JoinableTaskFactory.SwitchToMainThreadAsync(ct);
                       return new NavLanguageService(this);
                   }, true);

        AddService(typeof(NavLanguagePackage), async (_, ct, _)
                       => {
                       await JoinableTaskFactory.SwitchToMainThreadAsync(ct);
                       return this;
                   }, true);

        var componentModel = (IComponentModel) await GetServiceAsync(typeof(SComponentModel));

        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        // Cache schon mal vorwärmen...
        componentModel?.GetService<NavSolutionProvider>();

    }

    /// <summary>Das automationsseitige <c>_DTE</c>-Objekt der laufenden Visual-Studio-Instanz.</summary>
    public static _DTE DTE {
        get {
            _DTE dte = GetGlobalService<_DTE, _DTE>();
            return dte;
        }
    }

    /// <summary>
    /// Löst den globalen VS-Dienst zum Service-Typ <typeparamref name="TService"/> auf.
    /// </summary>
    /// <typeparam name="TService">Der Service-Typ (VS-Service-Schlüssel).</typeparam>
    /// <returns>Die Dienstinstanz oder <c>null</c>.</returns>
    public static object GetGlobalService<TService>() where TService : class {
        return GetGlobalService(typeof(TService));
    }

    /// <summary>
    /// Löst den globalen VS-Dienst zum Service-Typ <typeparamref name="TService"/> auf und castet ihn
    /// auf <typeparamref name="TInterface"/>.
    /// </summary>
    /// <typeparam name="TService">Der Service-Typ (VS-Service-Schlüssel).</typeparam>
    /// <typeparam name="TInterface">Die erwartete Schnittstelle.</typeparam>
    /// <returns>Die Dienstinstanz als <typeparamref name="TInterface"/> oder <c>null</c>.</returns>
    public static TInterface GetGlobalService<TService, TInterface>() where TInterface : class {
        return GetGlobalService(typeof(TService)) as TInterface;
    }

    /// <summary>
    /// Liefert den <see cref="IServiceProvider"/> des Package selbst (Service-Locator-Wurzel für die
    /// MEF-Dienstauflösung).
    /// </summary>
    static IServiceProvider GetServiceProvider() {
        var serviceProvider = GetGlobalService<NavLanguagePackage, IServiceProvider>();
        return serviceProvider;
    }

    /// <summary>Der registrierte <see cref="NavLanguageService"/> dieses Package.</summary>
    public static NavLanguageService Language => GetGlobalService<NavLanguageService, NavLanguageService>();

    /// <summary>Der Roslyn-<see cref="VisualStudioWorkspace"/> der laufenden VS-Instanz.</summary>
    public static VisualStudioWorkspace Workspace {
        get {
            var componentModel = GetGlobalService<SComponentModel, IComponentModel>();
            var workspace      = componentModel.GetService<VisualStudioWorkspace>();
            return workspace;
        }
    }

    /// <summary>Der globale <see cref="IServiceProvider"/> von Visual Studio.</summary>
    public static IServiceProvider ServiceProvider => GetGlobalService<IServiceProvider, IServiceProvider>();

    /// <summary>
    /// Führt den durch <paramref name="commandId"/> bezeichneten Befehl über den globalen
    /// <see cref="IMenuCommandService"/> aus (nur auf dem UI-Thread).
    /// </summary>
    /// <param name="commandId">Die auszuführende <see cref="CommandID"/>.</param>
    public static void InvokeCommand(CommandID commandId) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var oleCommandTarget = ServiceProvider.GetService(typeof(IMenuCommandService)) as IMenuCommandService;
        oleCommandTarget?.GlobalInvoke(commandId);

    }

    /// <summary>
    /// Setzt den Cursor im aktuellen <see cref="ITextSnapshot"/> auf <paramref name="location"/>, zentriert
    /// die Ansicht auf diese Position und übergibt den Fokus an die <see cref="ITextView"/>, damit der
    /// Nutzer sofort weitertippen kann.
    /// </summary>
    /// <param name="textView">Die Ziel-<see cref="ITextView"/>.</param>
    /// <param name="location">Der Zeichenindex im aktuellen Snapshot.</param>
    public static void NavigateToLocation(ITextView textView, int location) {

        var bufferPosition = new SnapshotPoint(textView.TextBuffer.CurrentSnapshot, location);

        textView.Caret.MoveTo(bufferPosition);
        textView.ViewScroller.EnsureSpanVisible(new SnapshotSpan(bufferPosition, 1), EnsureSpanVisibleOptions.AlwaysCenter);

        // ReSharper disable once SuspiciousTypeConversion.Global 
        (textView as Control)?.Focus();
    }

    /// <summary>
    /// Öffnet die Datei der <paramref name="location"/> im Vorschau-Tab und selektiert die durch die
    /// <see cref="Location"/> beschriebene Spanne.
    /// </summary>
    /// <param name="location">Das Sprungziel (Datei + Spanne); <c>null</c> wird toleriert.</param>
    /// <returns>
    /// Die geöffnete <see cref="IWpfTextView"/> oder <c>null</c>, wenn keine Datei geöffnet werden konnte.
    /// </returns>
    [CanBeNull]
    public static IWpfTextView GoToLocationInPreviewTab(Location location) {

        using (Logger.LogBlock(nameof(GoToLocationInPreviewTab))) {

            ThreadHelper.ThrowIfNotOnUIThread();

            if (location == null) {
                return null;
            }

            IWpfTextView wpfTextView = null;
            if (location.FilePath != null) {
                wpfTextView = OpenFileInPreviewTab(location.FilePath);
            }

            if (wpfTextView == null) {
                return null;
            }

            if (location.Start == 0 && location.Length == 0) {
                return wpfTextView;
            }

            var outliningManagerService = GetServiceProvider().GetMefService<IOutliningManagerService>();

            var snapshotSpan = location.ToSnapshotSpan(wpfTextView.TextSnapshot);
            if (wpfTextView.TryMoveCaretToAndEnsureVisible(snapshotSpan.Start, outliningManagerService)) {
                wpfTextView.SetSelection(snapshotSpan);
            }

            return wpfTextView;
        }
    }

    /// <summary>
    /// Öffnet <paramref name="file"/> als Dokument in Visual Studio und liefert dessen
    /// <see cref="IWpfTextView"/>.
    /// </summary>
    /// <param name="file">Der Pfad der zu öffnenden Datei.</param>
    /// <returns>Die <see cref="IWpfTextView"/> des Dokuments oder <c>null</c>.</returns>
    [CanBeNull]
    public static IWpfTextView OpenFile(string file) {

        using (Logger.LogBlock(nameof(OpenFile))) {

            ThreadHelper.ThrowIfNotOnUIThread();

            var serviceProvider = GetServiceProvider();

            Guid logicalView = Guid.Empty;
            VsShellUtilities.OpenDocument(serviceProvider, file, logicalView, out var _, out var _, out var windowFrame);

            return GetWpfTextViewFromFrame(windowFrame);
        }
    }

    /// <summary>
    /// Öffnet <paramref name="file"/> im Vorschau-Tab (provisorisches Dokument) und liefert dessen
    /// <see cref="IWpfTextView"/>.
    /// </summary>
    /// <param name="file">Der Pfad der zu öffnenden Datei.</param>
    /// <returns>Die <see cref="IWpfTextView"/> des Vorschau-Dokuments oder <c>null</c>.</returns>
    [CanBeNull]
    public static IWpfTextView OpenFileInPreviewTab(string file) {

        ThreadHelper.ThrowIfNotOnUIThread();

        using (Logger.LogBlock(nameof(OpenFileInPreviewTab))) {

            var state = __VSNEWDOCUMENTSTATE.NDS_Provisional; // | __VSNEWDOCUMENTSTATE.NDS_NoActivate;
            using (new NewDocumentStateScope(state, VSConstants.NewDocumentStateReason.Navigation)) {
                return OpenFile(file);
            }
        }
    }

    /// <summary>
    /// Liefert den <see cref="ITextBuffer"/> zu einer bereits in Visual Studio geöffneten Datei.
    /// </summary>
    /// <param name="filePath">Der Pfad der Datei.</param>
    /// <returns>Der <see cref="ITextBuffer"/> oder <c>null</c>, wenn die Datei nicht geöffnet ist.</returns>
    [CanBeNull]
    public static ITextBuffer GetOpenTextBufferForFile(string filePath) {

        using (Logger.LogBlock(nameof(GetOpenTextBufferForFile))) {

            var package = GetGlobalService<NavLanguagePackage, NavLanguagePackage>();

            var componentModel              = (IComponentModel) GetGlobalService(typeof(SComponentModel));
            var editorAdapterFactoryService = componentModel.GetService<IVsEditorAdaptersFactoryService>();

            if (VsShellUtilities.IsDocumentOpen(
                    package,
                    filePath,
                    Guid.Empty,
                    out IVsUIHierarchy _,
                    out uint _,
                    out IVsWindowFrame windowFrame)) {
                IVsTextView view = VsShellUtilities.GetTextView(windowFrame);
                if (view.GetBuffer(out var lines) == 0) {
                    if (lines is IVsTextBuffer buffer)
                        return editorAdapterFactoryService.GetDataBuffer(buffer);
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Ermittelt das Roslyn-<see cref="Project"/>, das die Datei <paramref name="filePath"/> enthält —
    /// über den DTE-Solution-Baum aufgelöst und im aktuellen Roslyn-Solution-Modell nachgeschlagen.
    /// </summary>
    /// <param name="filePath">Der Pfad der enthaltenen Datei.</param>
    /// <returns>Das enthaltende <see cref="Project"/> oder <c>null</c>, wenn keines gefunden wird.</returns>
    public static Project GetContainingProject(string filePath) {

        Dispatcher.CurrentDispatcher.VerifyAccess();

        var dteSolution = DTE.Solution;
        if (dteSolution == null) {
            Logger.Warn($"{nameof(GetContainingProject)}: There's no DTE solution");
            return null;
        }

        if (string.IsNullOrEmpty(filePath)) {
            Logger.Info($"{nameof(GetContainingProject)}: The text document has not path.");
            return null;
        }

        var projectItem = dteSolution.FindProjectItem(filePath);

        if (projectItem == null) {
            Logger.Warn($"{nameof(GetContainingProject)}: Unable to find a DTE project item with the path '{filePath}'");
            return null;
        }

        var containingProject = projectItem.ContainingProject;
        if (containingProject == null) {
            Logger.Warn($"{nameof(GetContainingProject)}: Project item with the path '{filePath}' has no containing project.");
            return null;
        }

        var projectPath = containingProject.FullName;
        if (string.IsNullOrEmpty(projectPath)) {
            Logger.Info($"{nameof(GetContainingProject)}: Containing project '{containingProject.Name}' for the item with the path '{filePath}' has no full path.");
            return null;
        }

        var roslynSolution = Workspace.CurrentSolution;

        var project = roslynSolution.Projects.FirstOrDefault(p => p.FilePath?.ToLower() == projectPath.ToLower());
        if (project == null) {
            Logger.Warn($"{nameof(GetContainingProject)}: Unable to find a roslyn project for the project '{projectPath.ToLower()}'.\nRoslyn Projects:\n{ProjectPaths(roslynSolution.Projects)}");
            return null;
        }

        return project;

        string ProjectPaths(IEnumerable<Project> projects) {
            return projects.Aggregate(new StringBuilder(), (sb, p) => sb.AppendLine(p.FilePath), sb => sb.ToString());
        }
    }

    /// <summary>
    /// Liefert die <see cref="IWpfTextView"/> des aktuell aktiven Dokuments.
    /// </summary>
    /// <returns>Die aktive <see cref="IWpfTextView"/> oder <c>null</c>, wenn keine ermittelbar ist.</returns>
    [CanBeNull]
    public static IWpfTextView GetActiveTextView() {

        ThreadHelper.ThrowIfNotOnUIThread();

        using (Logger.LogBlock(nameof(GetActiveTextView))) {

            var monitorSelection = (IVsMonitorSelection) GetGlobalService(typeof(SVsShellMonitorSelection));
            if (monitorSelection == null) {
                return null;
            }

            if (ErrorHandler.Failed(monitorSelection.GetCurrentElementValue((uint) VSConstants.VSSELELEMID.SEID_DocumentFrame, out var curDocument))) {
                Logger.Error("Get VSConstants.VSSELELEMID.SEID_DocumentFrame failed");
                return null;
            }

            if (!(curDocument is IVsWindowFrame frame)) {
                Logger.Error($"{nameof(curDocument)} ist kein {nameof(IVsWindowFrame)}");
                return null;
            }

            return GetWpfTextViewFromFrame(frame);
        }
    }

    /// <summary>
    /// Ermittelt die <see cref="IWpfTextView"/> zu einem <see cref="IVsWindowFrame"/> über den
    /// <see cref="IVsEditorAdaptersFactoryService"/>.
    /// </summary>
    /// <param name="frame">Der Fensterrahmen des Dokuments.</param>
    /// <returns>Die zugehörige <see cref="IWpfTextView"/> oder <c>null</c>.</returns>
    [CanBeNull]
    static IWpfTextView GetWpfTextViewFromFrame(IVsWindowFrame frame) {

        ThreadHelper.ThrowIfNotOnUIThread();

        using (Logger.LogBlock(nameof(GetWpfTextViewFromFrame))) {

            var textView = VsShellUtilities.GetTextView(frame);
            if (textView == null) {
                Logger.Warn($"{nameof(GetWpfTextViewFromFrame)}: {nameof(frame)} liefert null.");
                return null;
            }

            var model          = (IComponentModel)GetGlobalService(typeof(SComponentModel));
            var adapterFactory = model.GetService<IVsEditorAdaptersFactoryService>();
            var wpfTextView    = adapterFactory.GetWpfTextView(textView);
            return wpfTextView;

        }
    }

    /// <summary>
    /// Liefert die aktuelle Nav-<see cref="NavSolution"/> über den <see cref="NavSolutionProvider"/>.
    /// </summary>
    /// <param name="cancellationToken">Token zum Abbrechen der Ermittlung.</param>
    /// <returns>Die aktuelle <see cref="NavSolution"/> des Workspace.</returns>
    public static async Task<NavSolution> GetSolutionAsync(CancellationToken cancellationToken) {

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var solutionProvider = GetServiceProvider().GetMefService<NavSolutionProvider>();

        await TaskScheduler.Default;

        var solution = await solutionProvider.GetSolutionAsync(cancellationToken).ConfigureAwait(false);

        return solution;

    }

    //public static BitmapSource GetBitmapSource(ImageMoniker moniker, Color? backgroundColor = null) {

    //    ThreadHelper.ThrowIfNotOnUIThread();

    //    var imageAttributes = GetImageAttributes(_UIImageType.IT_Bitmap, _UIDataFormat.DF_WPF, backgroundColor);
    //    var imageService    = GetGlobalService<SVsImageService, IVsImageService2>();
    //    var result          = imageService?.GetImage(moniker, imageAttributes);

    //    object data = null;
    //    result?.get_Data(out data);
    //    return data as BitmapSource;
    //}

    //public static Bitmap GetBitmap(ImageMoniker moniker, Color? backgroundColor = null) {

    //    ThreadHelper.ThrowIfNotOnUIThread();

    //    var imageAttributes = GetImageAttributes(_UIImageType.IT_Bitmap, _UIDataFormat.DF_WinForms, backgroundColor);
    //    var imageService    = GetGlobalService<SVsImageService, IVsImageService2>();
    //    var result          = imageService?.GetImage(moniker, imageAttributes);

    //    object data = null;
    //    result?.get_Data(out data);
    //    return data as Bitmap;
    //}

    //public static IntPtr GetImageList(ImageMoniker moniker, Color? backgroundColor = null) {

    //    ThreadHelper.ThrowIfNotOnUIThread();

    //    var imageAttributes = GetImageAttributes(_UIImageType.IT_ImageList, _UIDataFormat.DF_Win32, backgroundColor);
    //    var imageService    = GetGlobalService<SVsImageService, IVsImageService2>();
    //    var result          = imageService?.GetImage(moniker, imageAttributes);

    //    if (!(Microsoft.Internal.VisualStudio.PlatformUI.Utilities.GetObjectData(result) is IVsUIWin32ImageList imageListData)) {
    //        Logger.Warn($"{nameof(GetImageList)}: Unable to get IVsUIWin32ImageList");
    //        return IntPtr.Zero;
    //    }

    //    if (!ErrorHandler.Succeeded(imageListData.GetHIMAGELIST(out var imageListInt))) {
    //        Logger.Warn($"{nameof(GetImageList)}: Unable to get HIMAGELIST");
    //        return IntPtr.Zero;

    //    }

    //    return (IntPtr) imageListInt;
    //}

    //static ImageAttributes GetImageAttributes(_UIImageType imageType, _UIDataFormat format, Color? backgroundColor, int width = 16, int height = 16) {

    //    ImageAttributes imageAttributes = new ImageAttributes {
    //        StructSize    = Marshal.SizeOf(typeof(ImageAttributes)),
    //        Dpi           = 96,
    //        Flags         = (uint) _ImageAttributesFlags.IAF_RequiredFlags,
    //        ImageType     = (uint) imageType,
    //        Format        = (uint) format,
    //        LogicalHeight = height,
    //        LogicalWidth  = width
    //    };
    //    if (backgroundColor.HasValue) {
    //        unchecked {
    //            imageAttributes.Flags |= (uint) _ImageAttributesFlags.IAF_Background;
    //        }

    //        imageAttributes.Background = (uint) backgroundColor.Value.ToArgb();
    //    }

    //    return imageAttributes;
    //}
        

}