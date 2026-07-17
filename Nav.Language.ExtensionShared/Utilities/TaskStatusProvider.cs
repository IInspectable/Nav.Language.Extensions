#region Using Directives

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TaskStatusCenter;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Utilities; 

/// <summary>
/// MEF-Dienst, der Einträge im Visual-Studio-Task-Status-Center anlegt. Kapselt den
/// <see cref="IVsTaskStatusCenterService"/> und gibt je Operation einen <see cref="TaskStatus"/> aus,
/// über den Fortschritt gemeldet und der Eintrag beendet wird.
/// </summary>
[Export(typeof(TaskStatusProvider))]
class TaskStatusProvider {

    readonly Lazy<IVsTaskStatusCenterService> _taskCenterService;

    /// <summary>
    /// MEF-Import-Konstruktor; löst den <see cref="IVsTaskStatusCenterService"/> verzögert aus dem
    /// VS-Service-Provider auf.
    /// </summary>
    /// <param name="serviceProvider">Der von MEF bereitgestellte VS-Service-Provider.</param>
    [ImportingConstructor]
    public TaskStatusProvider(SVsServiceProvider serviceProvider) {
        _taskCenterService = new Lazy<IVsTaskStatusCenterService>(
            () => (IVsTaskStatusCenterService) serviceProvider.GetService(typeof(SVsTaskStatusCenterService)));
    }

    /// <summary>
    /// Registriert einen neuen, nicht abbrechbaren Eintrag im Task-Status-Center und gibt den
    /// zugehörigen <see cref="TaskStatus"/> zurück.
    /// </summary>
    /// <param name="title">Der im Task-Status-Center angezeigte Titel.</param>
    public TaskStatus CreateTaskStatus(string title) {

        var options = new TaskHandlerOptions {
            Title                  = title,
            ActionsAfterCompletion = CompletionActions.None
        };

        var taskCompletionSource = new TaskCompletionSource<bool>();

        var handler = _taskCenterService.Value?.PreRegister(options, data: default);
        handler?.RegisterTask(taskCompletionSource.Task);

        return new TaskStatus(taskCompletionSource, handler);
    }

}