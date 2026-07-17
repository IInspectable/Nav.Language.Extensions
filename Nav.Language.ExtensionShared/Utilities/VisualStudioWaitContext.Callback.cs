using Microsoft.VisualStudio.Shell.Interop;

namespace Pharmatechnik.Nav.Language.Extension.Utilities; 

partial class VisualStudioWaitContext {

    /// <summary>
    /// Note: this is a COM interface, however it is also free threaded.  This is necessary and
    /// by design so that we can hear about cancellation happening from the wait dialog (which
    /// will happen on the background).
    /// </summary>
    class Callback : IVsThreadedWaitDialogCallback {

        readonly VisualStudioWaitContext _waitContext;

        /// <summary>
        /// Erzeugt den Callback für den übergebenen <paramref name="waitContext"/>.
        /// </summary>
        /// <param name="waitContext">Der Warte-Kontext, an den das Abbrechen weitergereicht wird.</param>
        public Callback(VisualStudioWaitContext waitContext) {
            _waitContext = waitContext;
        }

        /// <summary>
        /// Wird vom Wait-Dialog (ggf. auf einem Hintergrund-Thread) aufgerufen, wenn der Benutzer
        /// abbricht, und leitet dies an den <see cref="VisualStudioWaitContext"/> weiter.
        /// </summary>
        public void OnCanceled() {
            _waitContext.OnCanceled();
        }
    }
}