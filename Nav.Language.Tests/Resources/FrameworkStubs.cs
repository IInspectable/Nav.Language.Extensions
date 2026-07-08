using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL;
// ReSharper disable InconsistentNaming

namespace Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL {
    public interface IWFServiceBase : IWFService {
        INavCommand EscapeTask(TO to);
    }
}

namespace Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL {

    public abstract class StandardWFS : BaseWFService, IWFServiceBase {
        public virtual INavCommand EscapeTask(TO to) {
            return null;
        }
    }

    public abstract class StandardWFS<TState> : BaseWFService<TState>, IWFServiceBase {
        public virtual INavCommand EscapeTask(TO to) {
            return null;
        }
    }
}

namespace Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL {

    public sealed class TaskCall: INavCommandBody {

        public TaskCall(string nodeName, BeginTaskWrapper beginWrapper) {
            BeginWrapper = beginWrapper;
            NodeName     = nodeName;
        }
        public string NodeName { get; }
        public BeginTaskWrapper BeginWrapper;

    }

    public static class NavCommandBody {

        public static string ComposeUnexpectedTransitionMessage(string logicMethodName, INavCommandBody body) {

            return $"{logicMethodName} returned unexpected result '{OfTypeText(body)}'.";
        }

        static string OfTypeText(INavCommandBody body) {
            if (body == null) {
                return "null";
            }
            if (body is TaskCall taskCall) {
                return $"of task node {taskCall.NodeName}";
            }
            return $"of type {body.GetType().Name}";
        }

    }

    public interface INavCommand {
    }

    public interface IINIT_TASK : INavCommand {
    }
    public interface IINIT_TASK<T> : INavCommand {
    }

    public interface CANCEL : IINIT_TASK {
    }

    public interface TASK_RESULT : IINIT_TASK, INavCommand {
    }

    // Continuation (V2): Tagging-Interfaces, die die .Concat-Überladung wählen (§3.8/①).
    public interface INOT_A_TASK_BOUNDARY : INavCommand {
    }

    public interface ITASK_BOUNDARY : INavCommand {
    }

    // Konkretes, einwertiges Task-Result: vereint die Body-Welt (INavCommandBody) mit der Kommando-Welt
    // (IINIT_TASK, ITASK_BOUNDARY) — daher castfrei als ctx.Exit-Fabrik und als V1-TaskResult (§3.8/②).
    public class TASK_RESULT<TResult> : TASK_RESULT, INavCommandBody, ITASK_BOUNDARY {
    }

    public delegate IINIT_TASK BeginTaskWrapper();
    public delegate IINIT_TASK<TResult> BeginTaskWrapper<TResult>();
    public delegate INavCommand AfterDelegate1<ResultType>(ResultType result);
    public delegate INavCommand AfterDelegate2<ResultType, P1>(ResultType result, P1 p1);

    public class GOTO_TASK : IINIT_TASK, ITASK_BOUNDARY { }

    public class GOTO_GUI : IINIT_TASK {

        // Continuation: „View zeigen, dann in einen Folge-Task fortsetzen". Die einzige neue
        // Framework-API (§3.8/①). GotoGUI(to).Concat(OpenModalTask/GotoTask(…)) → TWO_STEP … IINIT_TASK.
        public TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY Concat(ITASK_BOUNDARY boundary) {
            return null;
        }

        public TWO_STEP_IINIT_TASK Concat(INOT_A_TASK_BOUNDARY body) {
            return null;
        }

    }

    public class OPEN_MODAL_GUI : IINIT_TASK {
    }

    public class START_NONMODAL_TASK : INavCommand {
    }

    public class START_MODAL_TASK : INavCommand, ITASK_BOUNDARY {
    }

    public class END : INavCommand, ITASK_BOUNDARY, INavCommandBody {
    }

    // Ergebnis von GOTO_GUI.Concat(…): ist selbst wieder IINIT_TASK (init-legal, §3.8/①).
    public class TWO_STEP_IINIT_TASK_TO_TASK_BOUNDARY : IINIT_TASK {
    }

    public class TWO_STEP_IINIT_TASK : IINIT_TASK {
    }

    public interface INavCommandBody {
    }

    public interface TO : INavCommandBody {
    }

    public interface IWFService {
    }

    public interface IClientSideWFS {
    }

}

namespace Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL {
    public interface IBeginWFService {
    }

    public abstract class BaseWFService : IWFService {

        // V2 typisiert die ctx.Exit-Fabrik konkret als TASK_RESULT<T> (castfrei); V1 nutzt es als
        // INavCommandBody (TASK_RESULT<T> ist beides, §3.8/②).
        public TASK_RESULT<TResult> InternalTaskResult<TResult>(TResult result) {
            return null;
        }

        public GOTO_GUI GotoGUI(TO to) {
            return null;
        }

        public OPEN_MODAL_GUI OpenModalGUI(TO to) {
            return null;
        }

        public START_NONMODAL_TASK StartNonModalGUI(TO to) {
            return null;
        }

        public START_MODAL_TASK OpenModalTask<TResult>(BeginTaskWrapper wrapped, AfterDelegate1<TResult> after) {
            return null;
        }

        public START_NONMODAL_TASK StartNonModalTask<TResult>(BeginTaskWrapper wrapped, AfterDelegate1<TResult> after) {
            return null;
        }

        public GOTO_TASK GotoTask<TResult>(BeginTaskWrapper wrapped, AfterDelegate1<TResult> after) {
            return null;
        }

        public GOTO_TASK GotoTask<TResult>(BeginTaskWrapper<TResult> wrapped, AfterDelegate1<TResult> after) {
            return null;
        }

        // V2: ctx.Cancel()/ctx.End() rufen diese Fabriken direkt (V1 reicht die Marker durch den Switch).
        public CANCEL Cancel() {
            return null;
        }

        public END EndNonModal() {
            return null;
        }
    }

    // ReSharper disable once UnusedTypeParameter
    public abstract class BaseWFService<TState> : BaseWFService {

    }
}

namespace Pharmatechnik.Apotheke.XTplus.Common.IWFL {
    public interface ILegacyMessageBoxWFS : IWFServiceBase, IWFService {
        INavCommand End(TO to);
    }
}
