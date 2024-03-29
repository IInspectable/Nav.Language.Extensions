#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.QuickInfo; 

[Export(typeof(IAsyncQuickInfoSourceProvider))]
[Name(QuickInfoSourceProviderNames.DebugQuickInfoSourceProvider)]
[Order(After = QuickInfoSourceProviderNames.DefaultQuickInfoPresenter)]
[ContentType(NavLanguageContentDefinitions.ContentType)]
class DebugQuickInfoSourceProvider: IAsyncQuickInfoSourceProvider {

    [ImportingConstructor]
    public DebugQuickInfoSourceProvider(ITextStructureNavigatorSelectorService navigatorService, ITextBufferFactoryService textBufferFactoryService, CodeContentControlProvider codeContentControlProvider) {
    }

    IAsyncQuickInfoSource IAsyncQuickInfoSourceProvider.TryCreateQuickInfoSource(ITextBuffer textBuffer) {
        return new DebugQuickInfoSource(textBuffer);
    }

}