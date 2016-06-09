using JetBrains.Annotations;

namespace Pharmatechnik.Nav.Language {
    public interface ITransition: IEdge {

        [NotNull]
        ITaskDefinitionSymbol TaskDefinition { get; }

        [NotNull]
        TransitionDefinitionSyntax Syntax { get; }
        
        [NotNull]
        IReadOnlySymbolCollection<ITriggerSymbol> Triggers { get; }
    }
}