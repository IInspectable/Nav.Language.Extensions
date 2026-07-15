namespace Pharmatechnik.Nav.Language.Extension.BraceMatching; 

/// <summary>
/// Name des VS-Standard-<see cref="Microsoft.VisualStudio.Text.Tagging.TextMarkerTag"/>s für die
/// Klammer-Hervorhebung. Der <see cref="BraceMatchingTagger"/> vergibt diesen Marker, damit VS die
/// zusammengehörigen Klammern in seiner Standarddarstellung hervorhebt.
/// </summary>
static class BraceMatchingTypeNames {
    /// <summary>Marker-Name der Klammer-Hervorhebung (<c>"brace matching"</c>).</summary>
    public const string BraceMatching = "brace matching";
}