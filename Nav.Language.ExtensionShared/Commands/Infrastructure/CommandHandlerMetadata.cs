#region Using Directives

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Utilities;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

/// <summary>
/// Die von MEF aus <see cref="ExportCommandHandlerAttribute"/> und <see cref="OrderAttribute"/>
/// eingesammelten Metadaten eines Command-Handlers. <see cref="CommandHandlerServiceProvider"/>
/// importiert die Handler samt dieser Metadaten (als <c>Lazy&lt;INavCommandHandler,
/// CommandHandlerMetadata&gt;</c>) und nutzt sie, um pro Content-Type die zuständigen Handler
/// auszuwählen und über <see cref="IOrderableMetadata"/> (<see cref="Before"/>/<see cref="After"/>)
/// zu reihen. Alle Werte werden gegen fehlende Metadaten auf leere, nicht-<c>null</c>-Werte
/// normalisiert.
/// </summary>
class CommandHandlerMetadata: IOrderableMetadata {
       
    /// <summary>
    /// Liest die Metadaten aus dem von MEF gelieferten Wörterbuch der Export-Metadaten;
    /// fehlende Einträge werden auf <see cref="String.Empty"/> bzw. leere Listen abgebildet.
    /// </summary>
    /// <param name="data">Die von MEF bereitgestellten Roh-Metadaten des Exports.</param>
    public CommandHandlerMetadata(IDictionary<string, object> data) {
        Name         = (string) data.GetValueOrDefault(nameof(ExportCommandHandlerAttribute.Name))                       ?? String.Empty;
        ContentTypes = (IReadOnlyList<string>)data.GetValueOrDefault(nameof(ExportCommandHandlerAttribute.ContentTypes)) ?? Array.Empty<string>();
        Before       = (IReadOnlyList<string>)data.GetValueOrDefault(nameof(OrderAttribute.Before))                      ?? Array.Empty<string>();
        After        = (IReadOnlyList<string>)data.GetValueOrDefault(nameof(OrderAttribute.After))                       ?? Array.Empty<string>();
    }

    /// <summary>Der eindeutige Name des Handlers (aus <see cref="ExportCommandHandlerAttribute.Name"/>).</summary>
    public string                Name         { get; }
    /// <summary>Die Content-Types, für die der Handler greift (aus <see cref="ExportCommandHandlerAttribute.ContentTypes"/>).</summary>
    public IReadOnlyList<string> ContentTypes { get; }
    /// <summary>Namen der Handler, vor denen dieser Handler gereiht werden soll (aus <see cref="OrderAttribute.Before"/>).</summary>
    public IReadOnlyList<string> Before       { get; }
    /// <summary>Namen der Handler, nach denen dieser Handler gereiht werden soll (aus <see cref="OrderAttribute.After"/>).</summary>
    public IReadOnlyList<string> After        { get; }
}