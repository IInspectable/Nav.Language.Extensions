#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

public static class RenameCodeFixProvider {

    public static IEnumerable<RenameCodeFix> SuggestCodeFixes(CodeFixContext context, CancellationToken cancellationToken = default) {
        return context.FindSymbols()
                      .Select(symbol => new Visitor(symbol, context).Visit(symbol))
                      .Where(codeFix => codeFix != null);
    }

    sealed class Visitor: SymbolVisitor<RenameCodeFix> {

        public Visitor(ISymbol originatingSymbol, CodeFixContext context) {
            OriginatingSymbol = originatingSymbol;
            Context           = context;
        }

        ISymbol        OriginatingSymbol { get; }
        CodeFixContext Context           { get; }

        public override RenameCodeFix VisitInitNodeSymbol(IInitNodeSymbol initNodeSymbol) {
            // Wenn es bereits einen Alias gibt, dann funktioniert der Rename nur auf dem Alias-Symbol
            if (OriginatingSymbol == initNodeSymbol && initNodeSymbol.Alias != null) {
                return DefaultVisit(initNodeSymbol);
            }

            return new InitNodeRenameCodeFix(initNodeSymbol, OriginatingSymbol, Context);
        }

        public override RenameCodeFix VisitInitNodeAliasSymbol(IInitNodeAliasSymbol initNodeAliasSymbol) {
            return new InitNodeRenameCodeFix(initNodeAliasSymbol.InitNode, OriginatingSymbol, Context);
        }

        public override RenameCodeFix VisitExitNodeSymbol(IExitNodeSymbol exitNodeSymbol) {
            return new ExitNodeRenameCodeFix(exitNodeSymbol, OriginatingSymbol, Context);
        }

        public override RenameCodeFix VisitTaskNodeSymbol(ITaskNodeSymbol taskNodeSymbol) {
            // Wenn es bereits einen Alias gibt, dann funktioniert der Rename nur auf dem Alias-Symbol
            if (OriginatingSymbol == taskNodeSymbol && taskNodeSymbol.Alias != null) {
                return DefaultVisit(taskNodeSymbol);
            }

            return new TaskNodeRenameCodeFix(taskNodeSymbol, OriginatingSymbol, Context);
        }

        public override RenameCodeFix VisitTaskNodeAliasSymbol(ITaskNodeAliasSymbol taskNodeAliasSymbol) {
            return new TaskNodeRenameCodeFix(taskNodeAliasSymbol.TaskNode, OriginatingSymbol, Context);
        }

        public override RenameCodeFix VisitChoiceNodeSymbol(IChoiceNodeSymbol choiceNodeSymbol) {
            return new ChoiceRenameCodeFix(choiceNodeSymbol, OriginatingSymbol, Context);
        }

        public override RenameCodeFix VisitDialogNodeSymbol(IDialogNodeSymbol dialogNodeSymbol) {
            return new DialogNodeRenameCodeFix(dialogNodeSymbol, OriginatingSymbol, Context);
        }

        public override RenameCodeFix VisitViewNodeSymbol(IViewNodeSymbol viewNodeSymbol) {
            return new ViewNodeRenameCodeFix(viewNodeSymbol, OriginatingSymbol, Context);
        }

        public override RenameCodeFix VisitTaskDeclarationSymbol(ITaskDeclarationSymbol taskDeclarationSymbol) {
            return new TaskDeclarationRenameCodeFix(taskDeclarationSymbol, OriginatingSymbol, Context);
        }

        public override RenameCodeFix VisitTaskDefinitionSymbol(ITaskDefinitionSymbol taskDefinitionSymbol) {
            if (taskDefinitionSymbol.AsTaskDeclaration == null) {
                return DefaultVisit(taskDefinitionSymbol);
            }

            return Visit(taskDefinitionSymbol.AsTaskDeclaration);
        }

        public override RenameCodeFix VisitNodeReferenceSymbol(INodeReferenceSymbol nodeReferenceSymbol) {
            if (nodeReferenceSymbol.Declaration == null) {
                return DefaultVisit(nodeReferenceSymbol);
            }

            return Visit(nodeReferenceSymbol.Declaration);
        }

    }

}