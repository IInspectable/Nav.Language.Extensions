#region Using Directives

using System;

using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;

using Pharmatechnik.Nav.Utilities.Logging;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Hilfsklasse, die einen Ereignis-Empfänger (Sink) an einen COM-Verbindungspunkt eines
/// OLE-Objekts anbindet und die Abmeldung über ein <see cref="IDisposable"/> kapselt.
/// </summary>
static class ComEventSink {

    /// <summary>
    /// Meldet den übergebenen Sink am Verbindungspunkt des OLE-Objekts für die COM-Schnittstelle
    /// <typeparamref name="T"/> an.
    /// </summary>
    /// <typeparam name="T">Die COM-Ereignisschnittstelle; muss ein Interface sein.</typeparam>
    /// <param name="obj">Das OLE-Objekt, das <see cref="IConnectionPointContainer"/> implementiert.</param>
    /// <param name="sink">Der Ereignis-Empfänger.</param>
    /// <returns>
    /// Ein <see cref="IDisposable"/>, dessen <see cref="IDisposable.Dispose"/> die Verbindung
    /// wieder trennt.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="T"/> ist kein Interface oder es existiert kein Verbindungspunkt dafür.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="obj"/> ist kein <see cref="IConnectionPointContainer"/>.
    /// </exception>
    public static IDisposable Advise<T>(object obj, T sink) where T : class {

        ThreadHelper.ThrowIfNotOnUIThread();

        if (!typeof(T).IsInterface) {
            throw new InvalidOperationException();
        }

        if (!(obj is IConnectionPointContainer connectionPointContainer)) {
            throw new ArgumentException("Not an IConnectionPointContainer", nameof(obj));
        }

        connectionPointContainer.FindConnectionPoint(typeof(T).GUID, out var connectionPoint);
        if (connectionPoint == null) {
            throw new InvalidOperationException("Could not find connection point for " + typeof(T).FullName);
        }

        connectionPoint.Advise(sink, out var cookie);

        return new ComEventSinkImpl(connectionPoint, cookie);
    }

    /// <summary>
    /// Hält Verbindungspunkt und Cookie einer aktiven Anmeldung und meldet den Sink beim
    /// <see cref="Dispose"/> wieder ab.
    /// </summary>
    sealed class ComEventSinkImpl : IDisposable {

        static readonly Logger Logger = Logger.Create(typeof(ComEventSinkImpl));

        readonly IConnectionPoint _connectionPoint;
        readonly uint             _cookie;
        bool                      _unadvised;

        /// <summary>
        /// Initialisiert die Abmelde-Hülle mit dem Verbindungspunkt und dem beim Anmelden
        /// erhaltenen Cookie.
        /// </summary>
        /// <param name="connectionPoint">Der COM-Verbindungspunkt.</param>
        /// <param name="cookie">Das von <c>Advise</c> zurückgegebene Anmelde-Token.</param>
        public ComEventSinkImpl(IConnectionPoint connectionPoint, uint cookie) {
            _connectionPoint = connectionPoint;
            _cookie          = cookie;
        }

        /// <summary>
        /// Trennt die Verbindung zum Verbindungspunkt; ein wiederholter Aufruf wird protokolliert
        /// und ignoriert.
        /// </summary>
        public void Dispose() {

            ThreadHelper.ThrowIfNotOnUIThread();

            if (_unadvised) {
                Logger.Error("Already unadvised.");
                return;
            }

            _connectionPoint.Unadvise(_cookie);
            _unadvised = true;
        }
    }
}