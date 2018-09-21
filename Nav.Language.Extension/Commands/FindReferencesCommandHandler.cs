﻿#region Using Directives

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;

using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.FindReferences;
using Pharmatechnik.Nav.Language.FindReferences;

using Task = System.Threading.Tasks.Task;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands {

    [Export(typeof(ICommandHandler))]
    [ContentType(NavLanguageContentDefinitions.ContentType)]
    [Name(CommandHandlerNames.FindReferencesCommandHandler)]
    class FindReferencesCommandHandler: ICommandHandler<FindReferencesCommandArgs> {

        private readonly FindReferencesPresenter _referencesPresenter;

        [ImportingConstructor]
        public FindReferencesCommandHandler(FindReferencesPresenter referencesPresenter) {
            _referencesPresenter = referencesPresenter;

        }

        public string DisplayName => "Find All References";

        public CommandState GetCommandState(FindReferencesCommandArgs args) {
            return args.TextView is IWpfTextView ? CommandState.Available : CommandState.Unavailable;
        }

        public bool ExecuteCommand(FindReferencesCommandArgs args, CommandExecutionContext executionContext) {

            var codeGenerationUnitAndSnapshot = GetCodeGenerationUnit(args.SubjectBuffer);
            var symbol                        = args.TextView.TryFindSymbolUnderCaret(codeGenerationUnitAndSnapshot);

            FindAllReferencesAsync(symbol).FileAndForget(DisplayName);

            return false;

        }

        async Task FindAllReferencesAsync(ISymbol symbol) {

            if (symbol == null) {
                return;
            }

            var context = _referencesPresenter.StartSearch();
            try {

                var args = new FindReferencesArgs(symbol, context, NavLanguagePackage.SearchDirectory);

                await ReferenceFinder.FindReferences(args).ConfigureAwait(false);

            } finally {
                await context.OnCompletedAsync();
            }

        }

        static CodeGenerationUnitAndSnapshot GetCodeGenerationUnit(ITextBuffer textBuffer) {

            var semanticModelService      = SemanticModelService.GetOrCreateSingelton(textBuffer);
            var generationUnitAndSnapshot = semanticModelService.UpdateSynchronously();

            return generationUnitAndSnapshot;
        }

    }

}