using System;

namespace Pharmatechnik.Nav.Language.Extension.Utilities; 

/// <summary>
/// Dienst zum Anzeigen einer Fortschritts-/Warte-UI während einer potenziell länger laufenden
/// Operation. Die VS-Implementierung <see cref="VisualStudioWaitIndicator"/> blendet dazu den
/// „Threaded Wait Dialog" ein. Als MEF-Dienst (<see cref="IWaitIndicator"/>) exportiert.
/// </summary>
interface IWaitIndicator {

    /// <summary>
    /// Zeigt die Warte-UI an, führt <paramref name="action"/> synchron auf dem aufrufenden Thread aus
    /// und schließt die UI wieder. Fängt ein benutzerseitiges Abbrechen ab.
    /// </summary>
    /// <param name="title">Titel der Warte-UI.</param>
    /// <param name="message">Anfänglicher Meldungstext.</param>
    /// <param name="allowCancel">Ob dem Benutzer ein Abbrechen angeboten wird.</param>
    /// <param name="action">Die auszuführende Operation; erhält den laufenden <see cref="IWaitContext"/>.</param>
    /// <returns>
    /// <see cref="WaitIndicatorResult.Completed"/> bei vollständigem Durchlauf, sonst
    /// <see cref="WaitIndicatorResult.Canceled"/>.
    /// </returns>
    WaitIndicatorResult Wait(string title, string message, bool allowCancel, Action<IWaitContext> action);

    /// <summary>
    /// Startet die Warte-UI und gibt den laufenden <see cref="IWaitContext"/> zurück; der Aufrufer
    /// steuert die Lebensdauer selbst (Schließen per <see cref="IDisposable.Dispose"/>).
    /// </summary>
    /// <param name="title">Titel der Warte-UI.</param>
    /// <param name="message">Anfänglicher Meldungstext.</param>
    /// <param name="allowCancel">Ob dem Benutzer ein Abbrechen angeboten wird.</param>
    IWaitContext StartWait(string title, string message, bool allowCancel);
}