#region Using Directives

using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

/// <summary>
/// Basisklasse für die Argumente eines an die Command-Handler verteilten Editor-Kommandos.
/// Ein <see cref="CommandArgs"/> bündelt die <see cref="TextView"/>, von der das Kommando ausging,
/// mit dem <see cref="SubjectBuffer"/> unter dem Cursor und dient zugleich als Typschlüssel, über
/// den <see cref="CommandHandlerService"/> den passenden <see cref="INavCommandHandler{T}"/>
/// auswählt. Konkrete Kommandos leiten davon ab (z.B. <see cref="ViewCodeCommandArgs"/>).
/// </summary>
abstract class CommandArgs {

    /// <summary>
    /// Der <see cref="ITextBuffer"/>, in dem sich der Cursor beim Auslösen des Kommandos befindet.
    /// </summary>
    public ITextBuffer SubjectBuffer { get; }

    /// <summary>
    /// Die <see cref="IWpfTextView"/>, von der dieses Kommando ausging.
    /// </summary>
    public IWpfTextView TextView { get; }

    /// <summary>
    /// Initialisiert die gemeinsamen Kommando-Argumente. Beide Angaben sind Pflicht;
    /// <c>null</c> wird mit <see cref="System.ArgumentNullException"/> abgewiesen.
    /// </summary>
    /// <param name="textView">Die auslösende <see cref="IWpfTextView"/>.</param>
    /// <param name="subjectBuffer">Der <see cref="ITextBuffer"/> unter dem Cursor.</param>
    protected CommandArgs(IWpfTextView textView, ITextBuffer subjectBuffer) {
        TextView      = textView      ?? throw new ArgumentNullException(nameof(textView));
        SubjectBuffer = subjectBuffer ?? throw new ArgumentNullException(nameof(subjectBuffer));
    }
}