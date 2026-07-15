#region Using Directives

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

/// <summary>
/// Exportiert eine Klasse als Nav-Command-Handler in die MEF-Komposition. Der Export erfolgt unter
/// dem Vertragstyp <see cref="INavCommandHandler"/>; die als <c>[MetadataAttribute]</c>
/// mitgeführten Angaben (<see cref="Name"/>, <see cref="ContentTypes"/>) werden von
/// <see cref="CommandHandlerServiceProvider"/> als <see cref="CommandHandlerMetadata"/> importiert
/// und zur Auswahl und Reihung der Handler herangezogen.
/// </summary>
[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class)]
class ExportCommandHandlerAttribute : ExportAttribute {
    /// <summary>
    /// Der (eindeutige) Name des Handlers — zugleich der Bezugspunkt für die per
    /// <c>Order</c>-Attribut ausgedrückte Reihung relativ zu anderen Handlern.
    /// </summary>
    public string              Name         { get; }
    /// <summary>
    /// Die Content-Types, für die dieser Handler zuständig ist (typischerweise der Nav-Content-Type).
    /// </summary>
    public IEnumerable<string> ContentTypes { get; }

    /// <summary>
    /// Erzeugt das Export-Attribut mit Handler-Namen und den zugeordneten Content-Types.
    /// </summary>
    /// <param name="name">Der eindeutige Name des Handlers.</param>
    /// <param name="contentTypes">Die Content-Types, für die der Handler greift.</param>
    public ExportCommandHandlerAttribute(string name, params string[] contentTypes) :
        base(typeof(INavCommandHandler)) {
        Name         = name;
        ContentTypes = contentTypes;
    }
}