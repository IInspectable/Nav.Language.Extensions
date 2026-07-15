#region Using Directives

using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.TextManager.Interop;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

/// <summary>
/// MEF-exportierter <see cref="IVsTextViewCreationListener"/>, der für jede neu erzeugte
/// interaktive Nav-Editor-Sicht (Content-Type <see cref="NavLanguageContentDefinitions.ContentType"/>)
/// ein <see cref="CommandTarget"/> verdrahtet und damit das Nav-Command-Handling in die Sicht
/// einklinkt. Das Target wird per Auto-Closing-Property an die Sicht gehängt, sodass es genau einmal
/// entsteht und mit der Sicht wieder freigegeben wird.
/// </summary>
[Export(typeof(IVsTextViewCreationListener))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[TextViewRole(PredefinedTextViewRoles.Interactive)]
sealed class TextViewCreationListener : IVsTextViewCreationListener {

    readonly ICommandHandlerServiceProvider  _commandHandlerServiceProvider;
    readonly IVsEditorAdaptersFactoryService _editorAdaptersFactory;

    /// <summary>
    /// MEF-Konstruktor; erhält den Editor-Adapter-Dienst und den
    /// <see cref="ICommandHandlerServiceProvider"/> injiziert.
    /// </summary>
    /// <param name="editorAdaptersFactory">Adapter zwischen VS-Textsicht und <see cref="IWpfTextView"/>.</param>
    /// <param name="commandHandlerServiceProvider">Liefert je Sicht den Command-Handler-Dienst.</param>
    [ImportingConstructor]
    public TextViewCreationListener(
        IVsEditorAdaptersFactoryService editorAdaptersFactory,
        ICommandHandlerServiceProvider commandHandlerServiceProvider) {

        _editorAdaptersFactory         = editorAdaptersFactory;
        _commandHandlerServiceProvider = commandHandlerServiceProvider;            
    }

    /// <summary>
    /// Von Visual Studio bei Erzeugung einer Textsicht aufgerufen; ermittelt die
    /// <see cref="IWpfTextView"/> und legt (idempotent) das zugehörige <see cref="CommandTarget"/> an.
    /// </summary>
    /// <param name="textView">Die neu erzeugte VS-Textsicht.</param>
    public void VsTextViewCreated(IVsTextView textView) {

        var wpfTextView = _editorAdaptersFactory.GetWpfTextView(textView);

        wpfTextView.GetOrCreateAutoClosingProperty(wpfTextView,
                                                   tv => new CommandTarget(tv, _commandHandlerServiceProvider, _editorAdaptersFactory)

        );
    }
}