using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

/// <summary>
/// Kommando-Argumente für „View Code" (VS-Standardkommando <c>ViewCode</c>) — das Sprungziel vom
/// <c>.nav</c>-Editor in den zugehörigen generierten C#-Code. Der Typ dient
/// <see cref="CommandHandlerService"/> als Schlüssel, der das Kommando dem passenden
/// <see cref="INavCommandHandler{T}"/> zuweist, und trägt selbst keine zusätzlichen Daten über
/// <see cref="CommandArgs"/> hinaus.
/// </summary>
class ViewCodeCommandArgs: CommandArgs {
    /// <summary>
    /// Initialisiert die Argumente für das „View Code"-Kommando.
    /// </summary>
    /// <param name="wpfTextView">Die auslösende <see cref="IWpfTextView"/>.</param>
    /// <param name="subjectBuffer">Der <see cref="ITextBuffer"/> unter dem Cursor.</param>
    public ViewCodeCommandArgs(IWpfTextView wpfTextView, ITextBuffer subjectBuffer) : base(wpfTextView, subjectBuffer) {
    }
}