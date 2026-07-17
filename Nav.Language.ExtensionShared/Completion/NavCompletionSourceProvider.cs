#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Utilities;

using Pharmatechnik.Nav.Language.Extension.QuickInfo;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion; 

/// <summary>
/// Der MEF-Provider der Nav-Completion-Quelle. Exportiert für den VS-SDK-Vertrag
/// <see cref="IAsyncCompletionSourceProvider"/> und über <see cref="NavLanguageContentDefinitions.ContentType"/>
/// auf den Nav-Inhaltstyp beschränkt; erzeugt je Ansicht eine <see cref="NavCompletionSource"/> und reicht
/// ihr den importierten <see cref="QuickinfoBuilderService"/> für die Tooltips durch.
/// </summary>
[Export(typeof(IAsyncCompletionSourceProvider))]
[ContentType(NavLanguageContentDefinitions.ContentType)]
[Name(nameof(NavCompletionSourceProvider))]
class NavCompletionSourceProvider: AsyncCompletionSourceProvider {

    [ImportingConstructor]
    public NavCompletionSourceProvider(QuickinfoBuilderService quickinfoBuilderService) {
        QuickinfoBuilderService = quickinfoBuilderService;
    }

    /// <summary>Der für die Completion-Tooltips durchgereichte QuickInfo-Dienst.</summary>
    public QuickinfoBuilderService QuickinfoBuilderService { get; }

    /// <summary>Erzeugt die <see cref="NavCompletionSource"/> für eine Ansicht.</summary>
    protected override IAsyncCompletionSource CreateCompletionSource() {
        return new NavCompletionSource(QuickinfoBuilderService);
    }

}