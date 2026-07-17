#region Using Directives

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

using Pharmatechnik.Nav.Language.Extension.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension; 

/// <summary>
/// MEF-exportierte Brücke zwischen der Visual-Studio-Solution und dem Nav-Workspace. Der Provider
/// überwacht das Solution-Verzeichnis (<see cref="FileSystemWatcher"/>) sowie Solution- und
/// Hierarchie-Events, hält einen aktuellen <see cref="NavSolutionSnapshot"/> vor und baut ihn bei
/// Änderungen — entprellt (Throttle) — neu auf. Konsumenten beziehen die aktuelle
/// <see cref="NavSolution"/> über <see cref="GetSolutionAsync"/>. Die Hierarchie-Event-Anbindung liegt
/// in der partiellen Datei <c>NavSolutionProvider.HierarchyEvents.cs</c>.
/// </summary>
[Export]
partial class NavSolutionProvider {

    /// <summary>Der VS-<see cref="SVsServiceProvider"/> zur Auflösung von Shell-Diensten.</summary>
    public SVsServiceProvider ServiceProvider { get; }

    private readonly TaskStatusProvider _taskStatusProvider;

    DirectoryInfo _directory;

    private DateTime _lastChanged = DateTime.Now;

    private NavSolutionSnapshot _navSolutionSnapshot;

    readonly FileSystemWatcher _fileSystemWatcher;

    /// <summary>
    /// Erzeugt den Provider, abonniert die Solution- und Dateisystem-Events, verdrahtet die entprellte
    /// Snapshot-Neuberechnung und bindet die Hierarchie-Events an.
    /// </summary>
    /// <param name="taskStatusProvider">Provider für die Fortschrittsanzeige langlaufender Aufgaben.</param>
    /// <param name="serviceProvider">Der VS-Service-Provider.</param>
    [ImportingConstructor]
    public NavSolutionProvider(TaskStatusProvider taskStatusProvider, SVsServiceProvider serviceProvider) {
            
        ThreadHelper.ThrowIfNotOnUIThread();

        ServiceProvider = serviceProvider;
            
        _taskStatusProvider = taskStatusProvider;

        _navSolutionSnapshot = NavSolutionSnapshot.Empty;
           
        SolutionEvents.OnAfterCloseSolution                  += OnAfterCloseSolution;
        SolutionEvents.OnAfterOpenSolution                   += OnAfterOpenSolution;
        SolutionEvents.OnAfterBackgroundSolutionLoadComplete += OnAfterBackgroundSolutionLoadComplete;

        _fileSystemWatcher = new FileSystemWatcher {
            Filter                = NavSolution.SearchFilter,
            IncludeSubdirectories = true,
            NotifyFilter          = NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.DirectoryName
        };

        _fileSystemWatcher.Renamed += OnFileSystemRenamed;
        _fileSystemWatcher.Created += OnFileSystemCreated;
        _fileSystemWatcher.Deleted += OnFileSystemDeleted;
        _fileSystemWatcher.Error   += OnFileSystemError;

        UpdateSearchDirectory();

        // TODO Dispose beim Beenden von Studio
        Observable.FromEventPattern<EventArgs>(handler => Invalidated += handler,
                                               handler => Invalidated -= handler)
                  .Throttle(TimeSpan.FromSeconds(2))
                  .Select(_ => Observable.FromAsync(async () => await CreateSolutionSnapshotAsync(_taskStatusProvider, _directory, CancellationToken.None)))
                  .Concat()
                  .Subscribe(TrySetSolutionSnapshot);

        ConnectHierarchyEvents();
    }

    /// <summary>
    /// Wird ausgelöst, sobald der gehaltene Snapshot ungültig wurde und (entprellt) neu berechnet werden
    /// soll.
    /// </summary>
    private event EventHandler<EventArgs> Invalidated;

    void OnAfterOpenSolution(object sender, OpenSolutionEventArgs e) {
        ThreadHelper.ThrowIfNotOnUIThread();
        UpdateSearchDirectory();
    }

    void OnAfterCloseSolution(object sender, EventArgs e) {
        ThreadHelper.ThrowIfNotOnUIThread();
        UpdateSearchDirectory();
    }

    void OnAfterBackgroundSolutionLoadComplete(object sender, EventArgs e) {
        ThreadHelper.ThrowIfNotOnUIThread();
        UpdateSearchDirectory();
    }

    /// <summary>
    /// Übernimmt das aktuelle <see cref="SolutionDirectory"/> als Suchverzeichnis, richtet den
    /// <see cref="FileSystemWatcher"/> entsprechend ein (oder deaktiviert ihn) und invalidiert den
    /// Snapshot.
    /// </summary>
    void UpdateSearchDirectory() {

        ThreadHelper.ThrowIfNotOnUIThread();

        _directory = SolutionDirectory;

        if (_directory == null) {
            _fileSystemWatcher.EnableRaisingEvents = false;
        } else {
            _fileSystemWatcher.Path                = _directory.FullName;
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        Invalidate();
    }

    void OnFileSystemDeleted(object sender, FileSystemEventArgs e) {
        Invalidate();
    }

    void OnFileSystemCreated(object sender, FileSystemEventArgs e) {
        Invalidate();
    }

    void OnFileSystemRenamed(object sender, RenamedEventArgs e) {
        Invalidate();
    }

    void OnFileSystemError(object sender, ErrorEventArgs e) {
        // Bei Pufferüberlauf gehen einzelne Change-Events verloren — der Snapshot
        // wäre danach still veraltet. Konservativ komplett invalidieren.
        Invalidate();
    }

    /// <summary>
    /// Verwirft den gehaltenen Snapshot (setzt ihn auf <see cref="NavSolutionSnapshot.Empty"/>), merkt
    /// den Änderungszeitpunkt und löst <see cref="Invalidated"/> aus.
    /// </summary>
    void Invalidate() {

        lock (_gate) {
            _lastChanged         = DateTime.Now;
            _navSolutionSnapshot = NavSolutionSnapshot.Empty;
        }

        OnInvalidated();
    }

    /// <summary>Löst das <see cref="Invalidated"/>-Ereignis aus.</summary>
    void OnInvalidated() {
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Liefert die aktuelle <see cref="NavSolution"/>. Ist der gehaltene Snapshot noch gültig, wird er
    /// direkt zurückgegeben; andernfalls wird synchron ein neuer berechnet und (sofern weiterhin aktuell)
    /// übernommen.
    /// </summary>
    /// <param name="cancellationToken">Token zum Abbrechen der Berechnung.</param>
    /// <returns>Die aktuelle <see cref="NavSolution"/>.</returns>
    public async Task<NavSolution> GetSolutionAsync(CancellationToken cancellationToken) {

        NavSolutionSnapshot snapshot;
        DateTime            lastChanged;
        lock (_gate) {
            snapshot    = _navSolutionSnapshot;
            lastChanged = _lastChanged;
        }

        if (snapshot.IsCurrent(_directory, lastChanged)) {
            return snapshot.Solution;
        }

        var solutionSnapshot = await CreateSolutionSnapshotAsync(_taskStatusProvider, _directory, cancellationToken);

        TrySetSolutionSnapshot(solutionSnapshot);

        return solutionSnapshot.Solution;
    }

    /// <summary>
    /// Baut auf einem Hintergrund-Thread einen neuen <see cref="NavSolutionSnapshot"/> für
    /// <paramref name="directory"/> auf (via <see cref="NavSolution.FromDirectoryAsync"/>); für ein leeres
    /// Verzeichnis <see cref="NavSolutionSnapshot.Empty"/>.
    /// </summary>
    /// <param name="taskStatusProvider">Provider für die Fortschrittsanzeige.</param>
    /// <param name="directory">Das zu durchsuchende Solution-Verzeichnis.</param>
    /// <param name="cancellationToken">Token zum Abbrechen.</param>
    /// <returns>Der neu berechnete Snapshot.</returns>
    static async Task<NavSolutionSnapshot> CreateSolutionSnapshotAsync(TaskStatusProvider taskStatusProvider, DirectoryInfo directory, CancellationToken cancellationToken) {

        await TaskScheduler.Default;

        if (String.IsNullOrEmpty(directory?.FullName)) {
            return NavSolutionSnapshot.Empty;
        }

        var creationTime = DateTime.Now;

        using var taskStatus = taskStatusProvider.CreateTaskStatus("Nav Solution Provider");

        await taskStatus.OnProgressChangedAsync("Searching for the edge of eternity");

        var solution = await NavSolution.FromDirectoryAsync(directory, cancellationToken);

        return new NavSolutionSnapshot(creationTime, solution);

    }

    private readonly object _gate = new();

    /// <summary>
    /// Übernimmt <paramref name="navSolutionSnapshot"/> als aktuellen Snapshot — aber nur, wenn er zum
    /// aktuellen Verzeichnis und Änderungszeitpunkt noch passt (verwirft veraltete Ergebnisse
    /// nebenläufiger Berechnungen).
    /// </summary>
    /// <param name="navSolutionSnapshot">Der zu übernehmende Snapshot.</param>
    void TrySetSolutionSnapshot(NavSolutionSnapshot navSolutionSnapshot) {

        lock (_gate) {

            if (!navSolutionSnapshot.IsCurrent(_directory, _lastChanged)) {
                return;
            }

            _navSolutionSnapshot = navSolutionSnapshot;
        }

    }

    /// <summary>Gibt an, ob in Visual Studio aktuell eine Solution geöffnet ist.</summary>
    static bool IsSolutionOpen {
        get {
            ThreadHelper.ThrowIfNotOnUIThread();

            var solution = NavLanguagePackage.GetGlobalService<SVsSolution, IVsSolution>();

            return ErrorHandler.Succeeded(solution.GetProperty((int) __VSPROPID.VSPROPID_IsSolutionOpen, out object value)) &&
                   value is true;
        }
    }

    /// <summary>
    /// Das Wurzelverzeichnis der aktuell geöffneten Solution oder <c>null</c>, wenn keine Solution offen
    /// ist bzw. kein Verzeichnis ermittelt werden kann.
    /// </summary>
    [CanBeNull]
    static DirectoryInfo SolutionDirectory {
        get {
            ThreadHelper.ThrowIfNotOnUIThread();

            var solution = NavLanguagePackage.GetGlobalService<SVsSolution, IVsSolution>();

            if (!IsSolutionOpen) {
                return null;
            }

            if (ErrorHandler.Succeeded(solution.GetSolutionInfo(out var solutionDirectory, out _, out _))) {
                if (String.IsNullOrWhiteSpace(solutionDirectory)) {
                    return null;
                }

                return new DirectoryInfo(solutionDirectory);
            }

            return null;
        }
    }

}