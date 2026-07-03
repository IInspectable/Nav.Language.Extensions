#nullable enable

namespace Pharmatechnik.Nav.Language;

public interface ISemanticModelProviderFactory {

    ISemanticModelProvider CreateProvider(ISyntaxProvider syntaxProvider);

}