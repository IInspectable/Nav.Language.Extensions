using Microsoft.VisualStudio.Shell;

namespace Pharmatechnik.Nav.Language.Extension.LanguageService; 

/// <summary>
/// Basis-<see cref="RegistrationAttribute"/> für die Registrierung einer einzelnen Boolean-Option des
/// klassischen (Legacy-)Nav-Language-Service in der VS-Registry. Beim Setup wird unter dem
/// Language-Service-Schlüssel der Wert <see cref="OptionName"/> = 1 geschrieben. Konkrete Optionen
/// leiten ab und geben nur den <see cref="OptionName"/> vor (z.B.
/// <see cref="ProvideShowBraceCompletionAttribute"/>, <see cref="ProvideShowDropdownBarOptionAttribute"/>).
/// </summary>
// Registering a Legacy Language Service:
// https://msdn.microsoft.com/en-us/library/bb166421.aspx
abstract class LanguageServiceOptionRegistrationAttribute: RegistrationAttribute {

    /// <summary>
    /// Schreibt beim Setup den Options-Wert (<see cref="OptionName"/> = 1) unter <see cref="KeyName"/>
    /// in die VS-Registry.
    /// </summary>
    /// <param name="context">Der von VS bereitgestellte Registrierungskontext.</param>
    public override void Register(RegistrationContext context) {
        using var serviceKey = context.CreateKey(KeyName);
        serviceKey.SetValue(OptionName, 1);
    }

    /// <summary>
    /// Entfernt keine Werte (der Schlüssel wird mit dem Language-Service selbst entfernt).
    /// </summary>
    /// <param name="context">Der von VS bereitgestellte Registrierungskontext.</param>
    public override void Unregister(RegistrationContext context) {
    }

    /// <summary>
    /// Registry-Schlüssel des Language-Service, unter dem die Option abgelegt wird.
    /// </summary>
    protected virtual string KeyName => $"Languages\\Language Services\\{NavLanguageContentDefinitions.LanguageName}";

    /// <summary>
    /// Name des zu setzenden Options-Werts; von der konkreten Ableitung vorgegeben.
    /// </summary>
    protected abstract string OptionName { get; }

}