#region Using Directives

using System.Runtime.InteropServices;

using JetBrains.Annotations;

using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands;

/// <summary>
/// Das Command-Target der Nav-Editor-Integration: implementiert <see cref="IOleCommandTarget"/> und
/// klinkt sich als Command-Filter in die <see cref="IWpfTextView"/> ein. Ankommende VS-Kommandos
/// (<see cref="Exec"/>/<see cref="QueryStatus"/>) werden nach Kommando-Gruppe und -Id aufgeschlüsselt
/// und über den <see cref="ICommandHandlerService"/> an den passenden
/// <see cref="INavCommandHandler{T}"/> verteilt; nicht behandelte Kommandos gehen an das nächste
/// Command-Target (<see cref="NextCommandTarget"/>) weiter. Die Implementierung ist auf mehrere
/// Teildateien verteilt (<c>Exec</c>, <c>QueryStatus</c>).
/// </summary>
partial class CommandTarget: IOleCommandTarget {

    /// <summary>
    /// Erzeugt das Command-Target für die angegebene <paramref name="wpfTextView"/>: registriert sich
    /// über den VS-Editor-Adapter als Command-Filter (und merkt sich das dabei zurückgegebene nächste
    /// Command-Target) und beschafft über <paramref name="commandHandlerServiceProvider"/> den zur
    /// Sicht passenden <see cref="ICommandHandlerService"/>.
    /// </summary>
    /// <param name="wpfTextView">Die Editor-Sicht, in die sich das Target einklinkt.</param>
    /// <param name="commandHandlerServiceProvider">Liefert den Command-Handler-Dienst für die Sicht.</param>
    /// <param name="editorAdaptersFactory">Adapter zwischen <see cref="IWpfTextView"/> und der VS-Textsicht.</param>
    public CommandTarget(IWpfTextView wpfTextView,
                         ICommandHandlerServiceProvider commandHandlerServiceProvider,
                         IVsEditorAdaptersFactoryService editorAdaptersFactory) {

        WpfTextView = wpfTextView;

        var vsTextView = editorAdaptersFactory.GetViewAdapter(WpfTextView);
        // ReSharper disable once PossibleNullReferenceException Lass krachen
        int returnValue = vsTextView.AddCommandFilter(this, out var nextCommandTarget);
        Marshal.ThrowExceptionForHR(returnValue);

        HandlerService    = commandHandlerServiceProvider.GetService(WpfTextView);
        NextCommandTarget = nextCommandTarget;
    }

    /// <summary>Die Editor-Sicht, für die dieses Command-Target Kommandos behandelt.</summary>
    public IWpfTextView           WpfTextView       { get; }
    /// <summary>Das in der Command-Filter-Kette nachgelagerte Target, an das nicht behandelte Kommandos weitergehen.</summary>
    public IOleCommandTarget      NextCommandTarget { get; }
    /// <summary>Der zur <see cref="WpfTextView"/> passende Dienst, der Kommandos auf die Handler verteilt.</summary>
    public ICommandHandlerService HandlerService    { get; }

    /// <summary>
    /// Liefert den <see cref="ITextBuffer"/> unter dem Cursor, für den ein Kommando behandelt werden
    /// soll, oder <c>null</c>, wenn dort kein passender Puffer liegt (dann geht das Kommando an
    /// <see cref="NextCommandTarget"/>). Überschreibbar, um das Ziel-Puffer-Verhalten anzupassen.
    /// </summary>
    [CanBeNull]
    protected virtual ITextBuffer GetSubjectBufferContainingCaret() {
        return WpfTextView.GetBufferContainingCaret();
    }

}