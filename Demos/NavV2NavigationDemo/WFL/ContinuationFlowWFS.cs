#region Using Directives

using NavV2Demo.IWFL;

#endregion

namespace NavV2Demo.WFL;

// Handgeschriebener Logic-Teil des Workflows — HIER wird die Begin↔After-Navigation getestet.
//
// Die beiden Trigger-Logiken unten enthalten die Continuation-Aufrufe
//   callContext.ShowHome(to).BeginWarn(...)   und   callContext.ShowHome(to).BeginDrill(...)
// Genau diese BeginXY-Aufrufstellen sind die Ziele/Quellen der GoTo-Sprünge:
//   * Cursor auf AfterWarnLogic  →  GoTo  →  findet den BeginWarn-Aufruf (und umgekehrt).
//   * Cursor auf AfterDrillLogic →  GoTo  →  findet den BeginDrill-Aufruf (und umgekehrt).
// ReSharper disable once UnusedMember.Global
// ReSharper disable once InconsistentNaming
public partial class ContinuationFlowWFS
{
    // Init: Home anzeigen.
    protected override Init1CallContext.Result BeginLogic(Init1CallContext callContext)
    {
        return callContext.ShowHome(new HomeTO());
    }

    // Rücksprung aus dem modalen Warn-Task → zurück auf Home.
    // (GoTo von hier führt zu OnShowWarnLogic → callContext.ShowHome(to).BeginWarn(...).)
    protected override AfterWarnCallContext.Result AfterWarnLogic(MsgResult result,
                                                                  AfterWarnCallContext callContext)
    {
        return callContext.ShowHome(new HomeTO());
    }

    // Rücksprung aus dem Drill-Task → zurück auf Home.
    // (GoTo von hier führt zu OnDrillDownLogic → callContext.ShowHome(to).BeginDrill(...).)
    protected override AfterDrillCallContext.Result AfterDrillLogic(DetailResult result,
                                                                    AfterDrillCallContext callContext)
    {
        return callContext.ShowHome(new HomeTO());
    }

    // Schließen → Task mit Ergebnis 'true' beenden.
    protected override OnCloseCallContext.Result OnCloseLogic(HomeTO to,
                                                              OnCloseCallContext callContext)
    {
        return callContext.Exit(true);
    }

    // o-^ : Home zeigen und modal in den Warn-Task fortsetzen.
    //       >>> Aufrufstelle BeginWarn — Sprungziel aus AfterWarnLogic. <<<
    protected override OnShowWarnCallContext.Result OnShowWarnLogic(HomeTO to,
                                                                    OnShowWarnCallContext callContext)
    {
        return callContext.ShowHome(to).BeginWarn("Achtung — bitte bestätigen!");
    }

    // Zweiter Continuation-Einstieg in Warn — ANDERE Logic-Methode, gleiche Sub-Task-Grenze.
    //       >>> Zweite Aufrufstelle BeginWarn — muss beim GoTo aus AfterWarnLogic mitgelistet werden. <<<
    protected override OnReWarnCallContext.Result OnReWarnLogic(HomeTO to,
                                                                OnReWarnCallContext callContext)
    {
        return callContext.ShowHome(to).BeginWarn("Erneuter Hinweis — nochmal bestätigen!");
    }

    // --^ : Home zeigen und per Goto in den Drill-Task navigieren.
    //       >>> Aufrufstelle BeginDrill — Sprungziel aus AfterDrillLogic. <<<
    protected override OnDrillDownCallContext.Result OnDrillDownLogic(HomeTO to,
                                                                      OnDrillDownCallContext callContext)
    {
        return callContext.ShowHome(to).BeginDrill(42);
    }

    // Trigger, der die Entscheidung anstößt: delegiert an die Choice Choice_Retry.
    protected override OnDecideCallContext.Result OnDecideLogic(HomeTO to,
                                                                OnDecideCallContext callContext)
    {
        return callContext.Choice_Retry("warn");
    }

    // Choice-Logic: entscheidet anhand des übergebenen Grundes zwischen den drei Choice-Ausgängen.
    // Die geteilte Logic existiert genau einmal — alle Quellen (hier OnDecide) delegieren hierher.
    protected override Choice_RetryCallContext.Result Choice_RetryLogic(string reason,
                                                                        Choice_RetryCallContext callContext)
    {
        switch (reason)
        {
            case "warn":
                // Continuation-Ausgang → DRITTE BeginWarn-Aufrufstelle, diesmal in der Choice-Logic.
                //       >>> Muss beim GoTo aus AfterWarnLogic mitgelistet werden. <<<
                return callContext.ShowHome(new HomeTO()).BeginWarn("Choice → Warnung anzeigen");

            case "abbruch":
                return callContext.Exit(false);

            default:
                return callContext.ShowHome(new HomeTO()); // plain: zurück auf Home (impliziter Result)
        }
    }
}