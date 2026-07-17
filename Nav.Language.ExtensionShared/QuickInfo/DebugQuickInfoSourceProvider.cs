#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.QuickInfo; 

/// <summary>
/// MEF-Provider der Debug-QuickInfo. Erfüllt den VS-SDK-Vertrag <see cref="IAsyncQuickInfoSourceProvider"/>,
/// ist über <see cref="NavLanguageContentDefinitions.ContentType"/> auf Nav beschränkt und nach dem
/// Standard-Presenter einsortiert; erzeugt je TextBuffer eine <see cref="DebugQuickInfoSource"/>.
/// </summary>
[Export(typeof(IAsyncQuickInfoSourceProvider))]
[Name(QuickInfoSourceProviderNames.DebugQuickInfoSourceProvider)]
[Order(After = QuickInfoSourceProviderNames.DefaultQuickInfoPresenter)]
[ContentType(NavLanguageContentDefinitions.ContentType)]
class DebugQuickInfoSourceProvider: IAsyncQuickInfoSourceProvider {

    [ImportingConstructor]
    public DebugQuickInfoSourceProvider(ITextStructureNavigatorSelectorService navigatorService, ITextBufferFactoryService textBufferFactoryService, CodeContentControlProvider codeContentControlProvider) {
    }

    /// <summary>VS-SDK-Vertrag: erzeugt die <see cref="DebugQuickInfoSource"/> für den gegebenen TextBuffer.</summary>
    IAsyncQuickInfoSource IAsyncQuickInfoSourceProvider.TryCreateQuickInfoSource(ITextBuffer textBuffer) {
        return new DebugQuickInfoSource(textBuffer);
    }

}