using System;
using System.Threading;

namespace Pharmatechnik.Nav.Language.Extension.Utilities; 

/// <summary>
/// Ergebnis eines über <see cref="IWaitIndicator"/> ausgeführten Warte-Vorgangs.
/// </summary>
enum WaitIndicatorResult {

    /// <summary>Die Aktion lief vollständig durch.</summary>
    Completed,
    /// <summary>Der Vorgang wurde vom Benutzer abgebrochen.</summary>
    Canceled,

}

/// <summary>
/// Laufender Warte-Vorgang: repräsentiert die dem Benutzer angezeigte Fortschritts-/Warte-UI (in VS
/// der „Threaded Wait Dialog") für die Dauer einer potenziell länger laufenden Operation. Wird über
/// <see cref="IWaitIndicator"/> gestartet und beim <see cref="IDisposable.Dispose"/> geschlossen.
/// </summary>
interface IWaitContext : IDisposable {

    /// <summary>
    /// Wird ausgelöst, sobald der Benutzer den Vorgang abbricht (nur wirksam bei <see cref="AllowCancel"/>).
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>Steuert, ob dem Benutzer ein Abbrechen angeboten wird.</summary>
    bool   AllowCancel { get; set; }
    /// <summary>Der aktuell angezeigte Meldungstext; ein Setzen aktualisiert die Warte-UI.</summary>
    string Message     { get; set; }

    /// <summary>Meldet einen Fortschritt an die Warte-UI.</summary>
    void UpdateProgress();

}