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
/// MEF-Provider der Symbol-QuickInfo (Hover). Erfüllt den VS-SDK-Vertrag <see cref="IAsyncQuickInfoSourceProvider"/>,
/// ist über <see cref="NavLanguageContentDefinitions.ContentType"/> auf Nav beschränkt und vor dem
/// Standard-Presenter einsortiert; erzeugt je TextBuffer eine <see cref="SymbolQuickInfoSource"/> und reicht
/// ihr den <see cref="QuickinfoBuilderService"/> durch.
/// </summary>
[Export(typeof(IAsyncQuickInfoSourceProvider))]
[Name(QuickInfoSourceProviderNames.SymbolQuickInfoSourceProvider)]
[Order(Before = QuickInfoSourceProviderNames.DefaultQuickInfoPresenter)]
[ContentType(NavLanguageContentDefinitions.ContentType)]
class SymbolQuickInfoSourceProvider: IAsyncQuickInfoSourceProvider {

    [ImportingConstructor]
    public SymbolQuickInfoSourceProvider(ITextStructureNavigatorSelectorService navigatorService,
                                         ITextBufferFactoryService textBufferFactoryService,
                                         CodeContentControlProvider codeContentControlProvider,
                                         QuickinfoBuilderService quickinfoBuilderService) {

        QuickinfoBuilderService = quickinfoBuilderService;
    }

    /// <summary>Der Dienst, der den QuickInfo-Inhalt (Symbol/Keyword) rendert; an die erzeugte Quelle durchgereicht.</summary>
    QuickinfoBuilderService QuickinfoBuilderService { get; }

    /// <summary>VS-SDK-Vertrag: erzeugt die <see cref="SymbolQuickInfoSource"/> für den gegebenen TextBuffer.</summary>
    IAsyncQuickInfoSource IAsyncQuickInfoSourceProvider.TryCreateQuickInfoSource(ITextBuffer textBuffer) {
        return new SymbolQuickInfoSource(textBuffer, QuickinfoBuilderService);
    }

}