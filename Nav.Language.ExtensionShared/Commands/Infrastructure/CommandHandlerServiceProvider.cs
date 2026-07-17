#region Using Directives

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

/// <summary>
/// Liefert für eine <see cref="ITextView"/> bzw. einen <see cref="ITextBuffer"/> den passend
/// zusammengestellten <see cref="ICommandHandlerService"/>.
/// </summary>
interface ICommandHandlerServiceProvider {

    /// <summary>
    /// Erzeugt einen <see cref="ICommandHandlerService"/> mit den für die Content-Types der
    /// <paramref name="textView"/> zuständigen, gereihten Handlern.
    /// </summary>
    ICommandHandlerService GetService(ITextView textView);
    /// <summary>
    /// Erzeugt einen <see cref="ICommandHandlerService"/> mit den für den Content-Type des
    /// <paramref name="textBuffer"/> zuständigen, gereihten Handlern.
    /// </summary>
    ICommandHandlerService GetService(ITextBuffer textBuffer);
}

/// <summary>
/// Die MEF-exportierte Standard-Implementierung von <see cref="ICommandHandlerServiceProvider"/>.
/// Importiert alle via <see cref="ExportCommandHandlerAttribute"/> exportierten
/// <see cref="INavCommandHandler"/> samt <see cref="CommandHandlerMetadata"/> und filtert sie je
/// Anfrage auf die zum Content-Type passenden, gereiht via <c>ExtensionOrderer</c>. Der
/// <see cref="CommandTarget"/> zieht sich über diesen Provider seinen
/// <see cref="ICommandHandlerService"/>.
/// </summary>
[Export(typeof(ICommandHandlerServiceProvider))]
class CommandHandlerServiceProvider : ICommandHandlerServiceProvider {

    readonly IEnumerable<Lazy<INavCommandHandler, CommandHandlerMetadata>> _commandHandlers;

    /// <summary>
    /// MEF-Konstruktor; erhält alle exportierten Command-Handler (lazy, samt Metadaten) injiziert.
    /// </summary>
    /// <param name="commandHandlers">Die per <see cref="ImportManyAttribute"/> eingesammelten Handler.</param>
    [ImportingConstructor]
    public CommandHandlerServiceProvider([ImportMany] IEnumerable<Lazy<INavCommandHandler, CommandHandlerMetadata>> commandHandlers) {
        _commandHandlers = commandHandlers;
    }

    /// <inheritdoc/>
    public ICommandHandlerService GetService(ITextView textView) {
        var contentTypes    = textView.GetContentTypes();
        var commandHandlers = SelectCommandHandler(contentTypes);
        return new CommandHandlerService(commandHandlers);
    }

    /// <inheritdoc/>
    public ICommandHandlerService GetService(ITextBuffer textBuffer) {
        var commandHandlers = SelectCommandHandler(textBuffer.ContentType);
        return new CommandHandlerService(commandHandlers);
    }

    /// <summary>
    /// Wählt die für die angegebenen Content-Types zuständigen Handler aus und reiht sie.
    /// </summary>
    IList<INavCommandHandler> SelectCommandHandler(params IContentType[] contentTypes) {
        return SelectCommandHandler((IEnumerable<IContentType>) contentTypes);
    }

    /// <summary>
    /// Filtert die importierten Handler auf jene, deren <see cref="CommandHandlerMetadata.ContentTypes"/>
    /// zu einem der <paramref name="contentTypes"/> passen, und bringt sie über <c>ExtensionOrderer</c>
    /// (unter Auswertung von <see cref="CommandHandlerMetadata.Before"/>/<see cref="CommandHandlerMetadata.After"/>)
    /// in die endgültige Reihenfolge.
    /// </summary>
    IList<INavCommandHandler> SelectCommandHandler(IEnumerable<IContentType> contentTypes) {

        var extensions = _commandHandlers.Where(h => contentTypes.Any(d => d.MatchesAny(h.Metadata.ContentTypes)));
                
        var handler = ExtensionOrderer.Order(extensions).Select(ch => ch.Value)
                                      .ToList();

        return handler;
    }
}