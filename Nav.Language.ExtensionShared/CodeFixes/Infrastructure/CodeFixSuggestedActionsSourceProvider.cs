#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Language.Intellisense;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

/// <summary>
/// Die per MEF für den Nav-Content-Type exportierte Fabrik der Lightbulb-Quelle
/// (<see cref="ISuggestedActionsSourceProvider"/>). Visual Studio ruft sie je Editor-View/Buffer auf; sie
/// erzeugt daraufhin eine an diesen View gebundene <see cref="CodeFixSuggestedActionsSource"/> und stattet
/// sie mit den geteilten Diensten (<see cref="ICodeFixSuggestedActionProviderService"/>,
/// Kategorie-Registry) aus.
/// </summary>
[Export(typeof(ISuggestedActionsSourceProvider))]
[Name(nameof(CodeFixSuggestedActionsSourceProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
internal class CodeFixSuggestedActionsSourceProvider: ISuggestedActionsSourceProvider {

    readonly ICodeFixSuggestedActionProviderService  _codeFixSuggestedActionProviderService;
    readonly ISuggestedActionCategoryRegistryService _suggestedActionCategoryRegistryService;

    /// <summary>Importiert die an die erzeugten Quellen weitergereichten Dienste über MEF.</summary>
    /// <param name="codeFixSuggestedActionProviderService">Die Aggregations-Fassade über alle Fix-Provider.</param>
    /// <param name="suggestedActionCategoryRegistryService">VS-Dienst zum Bilden von Kategorie-Sets.</param>
    [ImportingConstructor]
    public CodeFixSuggestedActionsSourceProvider(ICodeFixSuggestedActionProviderService codeFixSuggestedActionProviderService,
                                                 ISuggestedActionCategoryRegistryService suggestedActionCategoryRegistryService) {
        _codeFixSuggestedActionProviderService  = codeFixSuggestedActionProviderService;
        _suggestedActionCategoryRegistryService = suggestedActionCategoryRegistryService;
    }

    /// <summary>
    /// Erzeugt die an <paramref name="textView"/>/<paramref name="textBuffer"/> gebundene Lightbulb-Quelle,
    /// oder <c>null</c>, wenn weder View noch Buffer vorliegen.
    /// </summary>
    /// <param name="textView">Der Editor-View, für den Vorschläge angeboten werden.</param>
    /// <param name="textBuffer">Der zugehörige Textpuffer.</param>
    /// <returns>Eine neue <see cref="CodeFixSuggestedActionsSource"/>, oder <c>null</c>.</returns>
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