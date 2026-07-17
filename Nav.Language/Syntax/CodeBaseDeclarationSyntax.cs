using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die Code-Deklaration <c>[base …]</c> am Kopf einer <c>task</c>-Definition
/// (<see cref="TaskDefinitionSyntax.CodeBaseDeclaration"/>), z.B.
/// <c>[base StandardWFS&lt;TSType&gt; : IWFServiceBase, IBeginWFSType]</c> — legt die Basistypen des
/// generierten Codes fest: vor dem <c>:</c> die Basisklasse der WFSBase-Klasse
/// (<see cref="WfsBaseType"/>), dahinter optional das Basis-Interface des IWFS-Interfaces
/// (<see cref="IwfsBaseType"/>) und, komma-getrennt, das des IBeginWFS-Interfaces
/// (<see cref="IBeginWfsBaseType"/>). Fehlt die Deklaration bzw. eine Position, verwendet der
/// Codegenerator seine Defaults. Zulässig nur am Task-Definitions-Kopf (<see cref="CodeBlockFacts"/>).
/// </summary>
[Serializable]
[SampleSyntax("[base StandardWFS<TSType> : IWFServiceBase, IBeginWFSType]")]
public partial class CodeBaseDeclarationSyntax: CodeSyntax {

    readonly IReadOnlyList<CodeTypeSyntax> _baseTypes;

    internal CodeBaseDeclarationSyntax(TextExtent extent, IReadOnlyList<CodeTypeSyntax> baseTypes)
        : base(extent) {
        AddChildNodes(_baseTypes = baseTypes);
    }

    /// <summary>Das Schlüsselwort <c>base</c>.</summary>
    public SyntaxToken BaseKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.BaseKeyword);

    /// <summary>
    /// Die erste Typangabe (vor dem <c>:</c>): die Basisklasse der generierten WFSBase-Klasse.
    /// Bei vom Parser erzeugten Knoten stets vorhanden — die Grammatik verlangt mindestens eine
    /// Typangabe; <c>null</c> nur bei einer leeren <see cref="BaseTypes"/>-Liste.
    /// </summary>
    // TODO WfsBaseType dürfte eigentlich nie null sein?
    public CodeTypeSyntax? WfsBaseType {
        get {
            if (_baseTypes.Count == 0) {
                return null;
            }

            return _baseTypes[0];
        }
    }

    /// <summary>
    /// Die zweite Typangabe (hinter dem <c>:</c>): das Basis-Interface des generierten
    /// IWFS-Interfaces — <c>null</c>, wenn nicht angegeben (der Codegenerator nimmt dann seinen Default).
    /// </summary>
    public CodeTypeSyntax? IwfsBaseType {
        get {
            if (_baseTypes.Count < 2) {
                return null;
            }

            return _baseTypes[1];
        }
    }

    /// <summary>
    /// Die dritte Typangabe (hinter dem Komma): das Basis-Interface des generierten
    /// IBeginWFS-Interfaces — <c>null</c>, wenn nicht angegeben (der Codegenerator nimmt dann seinen Default).
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public CodeTypeSyntax? IBeginWfsBaseType {
        get {
            if (_baseTypes.Count < 3) {
                return null;
            }

            return _baseTypes[2];
        }
    }

    /// <summary>
    /// Alle Typangaben der Deklaration in Quelltext-Reihenfolge (höchstens drei, siehe
    /// <see cref="WfsBaseType"/>, <see cref="IwfsBaseType"/>, <see cref="IBeginWfsBaseType"/>).
    /// </summary>
    public IReadOnlyList<CodeTypeSyntax> BaseTypes => _baseTypes;

}
