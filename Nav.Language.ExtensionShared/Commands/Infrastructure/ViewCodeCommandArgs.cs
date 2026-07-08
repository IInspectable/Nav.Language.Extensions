using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

class ViewCodeCommandArgs: CommandArgs {
    public ViewCodeCommandArgs(IWpfTextView wpfTextView, ITextBuffer subjectBuffer) : base(wpfTextView, subjectBuffer) {
    }
}