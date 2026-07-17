namespace Pharmatechnik.Nav.Language.Extension; 

sealed partial class NavLanguagePackage {

    /// <summary>
    /// Die eindeutigen GUIDs der Nav-Extension als Zeichenketten-Konstanten (für die
    /// VS-Registrierungs-Attribute).
    /// </summary>
    public static class Guids {

        /// <summary>Die GUID des Nav-<see cref="LanguageService.NavLanguageService"/> (Sprache).</summary>
        public const string LanguageGuidString = "F997BDD8-C831-4069-9E0C-E26CE6C300C8";
        /// <summary>Die GUID des <see cref="NavLanguagePackage"/> (Package).</summary>
        public const string PackageGuidString  = "9B9FBDD6-3F79-4D5C-82E0-60C3546C9FEF";

    }

}