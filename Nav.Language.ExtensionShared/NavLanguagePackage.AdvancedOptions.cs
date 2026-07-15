#region Using Directives

using Microsoft.VisualStudio.Shell;
using Pharmatechnik.Nav.Language.Extension.Options;

#endregion

namespace Pharmatechnik.Nav.Language.Extension; 

[ProvideLanguageEditorOptionPage(
    pageType          : typeof(AdvancedOptionsDialogPage), 
    languageName      : NavLanguageContentDefinitions.LanguageName, 
    category          : null, 
    pageName          : AdvancedOptionsDialogPage.PageName, 
    pageNameResourceId: "#120")]
public sealed partial class NavLanguagePackage {

    /// <summary>
    /// Die erweiterten Editor-Optionen der Nav Language (<see cref="IAdvancedOptions"/>), bezogen über
    /// die registrierte <see cref="AdvancedOptionsDialogPage"/>.
    /// </summary>
    internal static IAdvancedOptions AdvancedOptions {
        get {

            var package = (NavLanguagePackage) GetGlobalService(typeof(NavLanguagePackage));

            return (AdvancedOptionsDialogPage) package.GetDialogPage(typeof(AdvancedOptionsDialogPage));
        }
    }

}