namespace Pharmatechnik.Nav.Language.Extension.LanguageService; 

/// <summary>
/// Registrierungsattribut, das dem Nav-Language-Service die Option „ShowBraceCompletion" zuschaltet —
/// die klammerbezogene Vervollständigung des klassischen Language-Service.
/// </summary>
sealed class ProvideShowBraceCompletionAttribute: LanguageServiceOptionRegistrationAttribute {

    /// <inheritdoc/>
    protected override string OptionName => "ShowBraceCompletion";

}