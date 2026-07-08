#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Language.Intellisense;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

[Export(typeof(ISuggestedActionsSourceProvider))]
[Name(nameof(CodeFixSuggestedActionsSourceProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
internal class CodeFixSuggestedActionsSourceProvider: ISuggestedActionsSourceProvider {

    readonly ICodeFixSuggestedActionProviderService  _codeFixSuggestedActionProviderService;
    readonly ISuggestedActionCategoryRegistryService _suggestedActionCategoryRegistryService;

    [ImportingConstructor]
    public CodeFixSuggestedActionsSourceProvider(ICodeFixSuggestedActionProviderService codeFixSuggestedActionProviderService,
                                                 ISuggestedActionCategoryRegistryService suggestedActionCategoryRegistryService) {
        _codeFixSuggestedActionProviderService  = codeFixSuggestedActionProviderService;
        _suggestedActionCategoryRegistryService = suggestedActionCategoryRegistryService;
    }

    public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer) {
        if (textBuffer == null && textView == null) {
            return null;
        }

        // TODO nur einzelne Textbuffer unterstützen?
        return new CodeFixSuggestedActionsSource(textBuffer,
                                                 _suggestedActionCategoryRegistryService,
                                                 _codeFixSuggestedActionProviderService,
                                                 textView);
    }

}