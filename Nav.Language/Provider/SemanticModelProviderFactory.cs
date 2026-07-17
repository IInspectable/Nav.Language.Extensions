namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Erzeugt <see cref="SemanticModelProvider"/>-Instanzen — die Standard-Factory für das nicht-cachende
/// semantische Modell.
/// </summary>
public class SemanticModelProviderFactory: ISemanticModelProviderFactory {

    /// <summary>Die gemeinsam nutzbare Standard-Factory.</summary>
    public static readonly ISemanticModelProviderFactory Default = new SemanticModelProviderFactory();

    /// <inheritdoc/>
    public ISemanticModelProvider CreateProvider(ISyntaxProvider syntaxProvider) {
        return new SemanticModelProvider(syntaxProvider);
    }

}