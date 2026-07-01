using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die Sprach-Versions-Direktive <c>#pragma version &lt;N&gt;</c> — die erste konkrete
/// <see cref="DirectiveTriviaSyntax"/>. Sie legt die <see cref="NavLanguageVersion"/> der Datei fest
/// (siehe <see cref="CodeGenerationUnitSyntax.LanguageVersion"/>). Steht sie am Kopf einer Datei, wird
/// sie strukturiert erkannt (kein <c>Nav3000</c>); ein fehlender oder nicht-ganzzahliger Versionswert
/// erzeugt <c>Nav3002</c>, und die Version fällt auf <see cref="NavLanguageVersion.Default"/> zurück.
/// </summary>
[Serializable]
[SampleSyntax("#pragma version 1")]
public sealed partial class VersionDirectiveSyntax: DirectiveTriviaSyntax {

    internal VersionDirectiveSyntax(TextExtent extent, NavLanguageVersion version): base(extent) {
        Version = version;
    }

    /// <summary>Das <c>pragma</c>-Schlüsselwort unmittelbar hinter dem <c>#</c>.</summary>
    public SyntaxToken PragmaKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.PragmaKeyword);

    /// <summary>
    /// Die von dieser Direktive festgelegte Sprach-Version. Bei fehlerhaftem Versionswert (bereits per
    /// <c>Nav3002</c> gemeldet) ist dies <see cref="NavLanguageVersion.Default"/>.
    /// </summary>
    public NavLanguageVersion Version { get; }

}
