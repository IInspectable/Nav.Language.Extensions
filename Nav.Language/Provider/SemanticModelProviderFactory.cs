namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Erzeugt <see cref="SemanticModelProvider"/>-Instanzen — die Standard-Fabrik für das nicht-cachende
/// semantische Modell.
/// </summary>
public class SemanticModelProviderFactory: ISemanticModelProviderFactory {

    /// <summary>Die gemeinsam nutzbare Standard-Fabrik.</summary>
    public static readonly ISemanticModelProviderFactory Default = new SemanticModelProviderFactory();

    /// <inheritdoc/>
    public ISemanticModelProvider CreateProvider(ISyntaxProvider syntaxProvider) {
        return new SemanticModelProvider(syntaxProvider);
    }

}