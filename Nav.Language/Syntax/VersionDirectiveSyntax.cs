using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die Sprach-Versions-Direktive <c>#version &lt;N&gt;</c> — die erste konkrete
/// <see cref="DirectiveTriviaSyntax"/>. Sie legt die <see cref="NavLanguageVersion"/> der Datei fest
/// (siehe <see cref="CodeGenerationUnitSyntax.LanguageVersion"/>). Steht sie am Kopf einer Datei, wird
/// sie strukturiert erkannt (kein <c>Nav3000</c>); ein fehlender oder nicht-ganzzahliger Versionswert
/// erzeugt <c>Nav3002</c>, und die Version fällt auf <see cref="NavLanguageVersion.Default"/> zurück.
/// </summary>
[Serializable]
[SampleSyntax("#version 1")]
public sealed partial class VersionDirectiveSyntax: DirectiveTriviaSyntax {

    internal VersionDirectiveSyntax(TextExtent extent, NavLanguageVersion version): base(extent) {
        Version = version;
    }

    /// <summary>Das <c>version</c>-Schlüsselwort unmittelbar hinter dem <c>#</c>.</summary>
    public SyntaxToken VersionKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.VersionKeyword);

    /// <summary>
    /// Das numerische Versionswert-Token (<see cref="SyntaxTokenType.PreprocessorNumber"/>) hinter
    /// <c>version</c>, oder ein fehlendes Token, wenn der Wert fehlt oder nicht-numerisch ist (in diesem Fall
    /// liegt bereits <c>Nav3002</c> vor und <see cref="Version"/> ist <see cref="NavLanguageVersion.Default"/>).
    /// </summary>
    public SyntaxToken VersionNumber => ChildTokens().FirstOrMissing(SyntaxTokenType.PreprocessorNumber);

    /// <summary>
    /// Die von dieser Direktive festgelegte Sprach-Version. Bei fehlerhaftem Versionswert (bereits per
    /// <c>Nav3002</c> gemeldet) ist dies <see cref="NavLanguageVersion.Default"/>.
    /// </summary>
    public NavLanguageVersion Version { get; }

}
