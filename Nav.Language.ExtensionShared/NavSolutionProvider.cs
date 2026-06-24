#region Using Directives

using System;
using System.Collections.Immutable;
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

[Export]
partial class NavSolutionProvider {

    public SVsServiceProvider ServiceProvider { get; }

    private readonly TaskStatusProvider _taskStatusProvider;

    DirectoryInfo _directory;

    private DateTime _lastChanged = DateTime.Now;

    private NavSolutionSnapshot _navSolutionSnapshot;

    readonly FileSystemWatcher _fileSystemWatcher;

    static string SearchFilter => $"*{NavLanguageContentDefinitions.FileExtension}";

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

    void Invalidate() {

        lock (_gate) {
            _lastChanged         = DateTime.Now;
            _navSolutionSnapshot = NavSolutionSnapshot.Empty;
        }

        OnInvalidated();
    }

    void OnInvalidated() {
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    public async Task<NavSolution> GetSolutionAsync(CancellationToken cancellationToken) {

        if (_navSolutionSnapshot.IsCurrent(_directory, _lastChanged)) {
            return _navSolutionSnapshot.Solution;
        }

        var solutionSnapshot = await CreateSolutionSnapshotAsync(_taskStatusProvider, _directory, cancellationToken);

        TrySetSolutionSnapshot(solutionSnapshot);

        return solutionSnapshot.Solution;
    }

    static async Task<NavSolutionSnapshot> CreateSolutionSnapshotAsync(TaskStatusProvider taskStatusProvider, DirectoryInfo directory, CancellationToken cancellationToken) {

        await TaskScheduler.Default;

        if (String.IsNullOrEmpty(directory?.FullName)) {
            return NavSolutionSnapshot.Empty;
        }

        var creationTime = DateTime.Now;
        // ReSharper disable once CollectionNeverQueried.Local
        var itemBuilder  = ImmutableArray.CreateBuilder<FileInfo>();

        using var taskStatus = taskStatusProvider.CreateTaskStatus("Nav Solution Provider");

        await taskStatus.OnProgressChangedAsync("Searching for the edge of eternity");

        foreach (var file in Directory.EnumerateFiles(directory.FullName,
                                                      SearchFilter,
                                                      SearchOption.AllDirectories)) {

            if (cancellationToken.IsCancellationRequested) {
                return NavSolutionSnapshot.Empty;
            }

            var fileInfo = new FileInfo(file);
                    
            itemBuilder.Add(fileInfo);

        }

        var solution = await NavSolution.FromDirectoryAsync(directory, cancellationToken);

        return new NavSolutionSnapshot(creationTime, solution);

    }

    private readonly object _gate = new();

    void TrySetSolutionSnapshot(NavSolutionSnapshot navSolutionSnapshot) {

        lock (_gate) {

            if (!navSolutionSnapshot.IsCurrent(_directory, _lastChanged)) {
                return;
            }

            _navSolutionSnapshot = navSolutionSnapshot;
        }

    }

    static bool IsSolutionOpen {
        get {
            ThreadHelper.ThrowIfNotOnUIThread();

            var solution = NavLanguagePackage.GetGlobalService<SVsSolution, IVsSolution>();
            solution.GetProperty((int) __VSPROPID.VSPROPID_IsSolutionOpen, out object value);

            return value is true;
        }
    }

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