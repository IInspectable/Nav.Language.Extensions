// -----------------------------------------------------------------------------------------------
// MINIMALER STUB des Pharmatechnik-XTplus-Frameworks.
//
// Dieses Demo lebt ausserhalb des echten Framework-Repos. Damit der aus Demo.nav generierte
// V2-Code (WFSBase, CallContexts, Continuation-Builder) UEBERHAUPT kompiliert, braucht es die
// Typen, gegen die er bindet: StandardWFS mit GotoGUI/OpenModalTask/GotoTask/Cancel/…, die
// Kommando-Interfaces (INavCommand / IINIT_TASK / ITASK_BOUNDARY) sowie .Concat(...).
//
// WICHTIG: Das ist NUR eine Kompilier-Attrappe — keine echte Navigations-Laufzeit. Es geht einzig
// darum, dass Roslyn ein vollstaendiges, fehlerfreies Semantik-Modell hat, damit die GoTo-Features
// der VS-Extension (Begin↔After) sauber greifen. Zur Laufzeit navigiert hier nichts.
// -----------------------------------------------------------------------------------------------

using System;

namespace Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL {

    /// <summary>Basis aller Navigations-Kommandos (Ergebnis eines Trigger-/Exit-Pfades).</summary>
    public interface INavCommand { }

    /// <summary>Init-legales Kommando (Ergebnis eines Init-Pfades). Ist zugleich ein <see cref="INavCommand"/>.</summary>
    public interface IINIT_TASK: INavCommand { }

    /// <summary>Task-Grenze (Ergebnis von OpenModalTask/GotoTask), Concat-Baustein einer Continuation.</summary>
    public interface ITASK_BOUNDARY: INavCommand { }

    /// <summary>Client-seitige WFS-Marke (Konstruktor-Parameter der generierten WFS).</summary>
    public interface IClientSideWFS { }
}

namespace Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL {

    using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL;

    /// <summary>
    /// Ergebnis von <c>GotoGUI(to)</c>. Traegt die einzige neue V2-Framework-API <c>.Concat(...)</c>,
    /// mit der eine Continuation (View zeigen UND in einen Folge-Task fortsetzen) gebaut wird.
    /// </summary>
    public sealed class GOTO_GUI: IINIT_TASK {
        public IINIT_TASK Concat(ITASK_BOUNDARY boundary) => new NavResult();
    }

    /// <summary>Generische Kommando-Attrappe fuer Cancel/InternalTaskResult/Concat-Ergebnisse.</summary>
    internal sealed class NavResult: IINIT_TASK { }

    /// <summary>Task-Grenzen-Attrappe (OpenModalTask/GotoTask-Ergebnis).</summary>
    internal sealed class TaskBoundary: ITASK_BOUNDARY { }
}

namespace Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL {

    /// <summary>Basis-Interface aller generierten <c>I{Task}WFS</c>-Dienste.</summary>
    public interface IWFServiceBase { }

    /// <summary>Basis-Interface aller generierten <c>IBegin{Task}WFS</c>-Einstiege.</summary>
    public interface IBeginWFService { }
}

namespace Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL {

    using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL;
    using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL;

    /// <summary>
    /// Basisklasse aller Workflow-Services (via <c>[base StandardWFS : IWFServiceBase]</c> im .nav).
    /// Stellt die Navigations-Primitive bereit, die der generierte CallContext-Code aufruft.
    /// </summary>
    public abstract class StandardWFS {

        protected StandardWFS() { }

        /// <summary>Zeigt einen GUI-Knoten (View/Dialog). Traegt <c>.Concat(...)</c> fuer Continuations.</summary>
        protected GOTO_GUI GotoGUI(object transferObject) => new GOTO_GUI();

        /// <summary>Bricht den aktuellen Workflow ab.</summary>
        protected IINIT_TASK Cancel() => new NavResult();

        /// <summary>Beendet den Task mit dem angegebenen Ergebnis (<c>[result …]</c>).</summary>
        protected INavCommand InternalTaskResult(object result) => new NavResult();

        /// <summary>Startet einen Folge-Task modal; <paramref name="after"/> ist der Rueckkehr-Einstieg.</summary>
        protected ITASK_BOUNDARY OpenModalTask<TResult>(Func<IINIT_TASK> begin, Func<TResult, INavCommand> after)
            => new TaskBoundary();

        /// <summary>Navigiert per Goto in einen Folge-Task; <paramref name="after"/> ist der Rueckkehr-Einstieg.</summary>
        protected ITASK_BOUNDARY GotoTask<TResult>(Func<IINIT_TASK> begin, Func<TResult, INavCommand> after)
            => new TaskBoundary();
    }
}
