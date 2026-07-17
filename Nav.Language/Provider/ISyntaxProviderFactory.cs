namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Erzeugt <see cref="ISyntaxProvider"/>-Instanzen — erlaubt es Aufrufern, eine (ggf. cachende)
/// Provider-Strategie zu wählen, ohne die konkrete Klasse zu kennen.
/// </summary>
public interface ISyntaxProviderFactory {

    /// <summary>Erzeugt einen neuen <see cref="ISyntaxProvider"/>.</summary>
    ISyntaxProvider CreateProvider();

}