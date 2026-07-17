#region Using Directives

using System;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TaskStatusCenter;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Utilities; 

/// <summary>
/// Repräsentiert einen laufenden Eintrag im Visual-Studio-Task-Status-Center (die Fortschritts-Leiste
/// unten links). Kapselt den <see cref="ITaskHandler"/> und die <see cref="TaskCompletionSource{TResult}"/>,
/// deren Abschluss dem Task-Status-Center das Ende der Operation signalisiert. Wird vom
/// <see cref="TaskStatusProvider"/> erzeugt; das <see cref="Dispose"/> bzw. <see cref="OnCompletedAsync"/>
/// beendet den Eintrag.
/// </summary>
class TaskStatus: IDisposable {

    readonly TaskCompletionSource<bool> _taskCompletionSource;

    ITaskHandler _taskHandler;

    /// <summary>
    /// Erzeugt den Status-Eintrag.
    /// </summary>
    /// <param name="taskCompletionSource">Signal-Quelle, deren Abschluss das Ende des Eintrags meldet.</param>
    /// <param name="taskHandler">Der VS-Task-Handler, an den Fortschritt gemeldet wird.</param>
    public TaskStatus(TaskCompletionSource<bool> taskCompletionSource,
                      ITaskHandler taskHandler) {
        _taskCompletionSource = taskCompletionSource;
        _taskHandler          = taskHandler;

    }

    /// <summary>
    /// Meldet einen neuen (nicht abbrechbaren, unbestimmten) Fortschritts-Text an das Task-Status-Center.
    /// </summary>
    /// <param name="message">Der anzuzeigende Fortschritts-Text.</param>
    public Task OnProgressChangedAsync(string message) {
        var data = new TaskProgressData {
            CanBeCanceled   = false,
            PercentComplete = null,
            ProgressText    = message
        };

        _taskHandler?.Progress.Report(data);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Beendet den Status-Eintrag (entspricht <see cref="Dispose"/>).
    /// </summary>
    public Task OnCompletedAsync() {
        Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Schließt den Eintrag ab und entfernt ihn aus dem Task-Status-Center.
    /// </summary>
    public void Dispose() {
        _taskCompletionSource.SetResult(true);
        _taskHandler = null;
    }

}