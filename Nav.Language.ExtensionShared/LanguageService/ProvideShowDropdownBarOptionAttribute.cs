namespace Pharmatechnik.Nav.Language.Extension.LanguageService; 

/// <summary>
/// Registrierungsattribut, das dem Nav-Language-Service die Option „ShowDropdownBarOption" zuschaltet —
/// damit bietet VS die Navigationsleiste (Dropdown-Bar) für Nav-Dateien an, deren Client die
/// <see cref="NavigationBar.NavigationBar"/> ist.
/// </summary>
sealed class ProvideShowDropdownBarOptionAttribute: LanguageServiceOptionRegistrationAttribute {

    /// <inheritdoc/>
    protected override string OptionName => "ShowDropdownBarOption";

}