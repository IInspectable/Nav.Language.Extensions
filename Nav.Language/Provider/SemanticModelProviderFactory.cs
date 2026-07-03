namespace Pharmatechnik.Nav.Language;

public class SemanticModelProviderFactory: ISemanticModelProviderFactory {

    public static readonly ISemanticModelProviderFactory Default = new SemanticModelProviderFactory();

    public ISemanticModelProvider CreateProvider(ISyntaxProvider syntaxProvider) {
        return new SemanticModelProvider(syntaxProvider);
    }

}