using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Abstrakte Basisklasse aller Typ-Angaben in Nav-Code-Annotationen — konkrete Ausprägungen sind
/// <see cref="SimpleTypeSyntax"/> (<c>int?</c>), <see cref="GenericTypeSyntax"/>
/// (<c>List&lt;string&gt;</c>) und <see cref="ArrayTypeSyntax"/> (<c>string[]</c>). Vorkommen: als
/// Parametertyp eines <see cref="ParameterSyntax"/> (in <c>[params …]</c> und <c>[result …]</c>)
/// sowie als Basistyp-Angabe in <see cref="CodeBaseDeclarationSyntax"/> (<c>[base …]</c>).
/// </summary>
[Serializable]
public abstract class CodeTypeSyntax: SyntaxNode {

    /// <summary>Initialisiert den Knoten mit dem von ihm abgedeckten Quelltext-Ausschnitt.</summary>
    /// <param name="extent">Der Quelltext-Ausschnitt, den dieser Typ-Knoten abdeckt.</param>
    protected CodeTypeSyntax(TextExtent extent): base(extent) {
    }

}