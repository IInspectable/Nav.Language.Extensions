#nullable enable

using Pharmatechnik.Nav.Language.CodeGen;

namespace Pharmatechnik.Nav.Language;

public interface IPathProviderFactory {

    IPathProvider CreatePathProvider(ITaskDefinitionSymbol taskDefinition, GenerationOptions options);

}