#nullable enable

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

[Serializable]
[SampleSyntax("[base StandardWFS<TSType> : IWFServiceBase, IBeginWFSType]")]
public partial class CodeBaseDeclarationSyntax: CodeSyntax {

    readonly IReadOnlyList<CodeTypeSyntax> _baseTypes;

    internal CodeBaseDeclarationSyntax(TextExtent extent, IReadOnlyList<CodeTypeSyntax> baseTypes)
        : base(extent) {
        AddChildNodes(_baseTypes = baseTypes);
    }

    public SyntaxToken BaseKeyword => ChildTokens().FirstOrMissing(SyntaxTokenType.BaseKeyword);

    // TODO WfsBaseType dürfte eigentlich nie null sein?
    public CodeTypeSyntax? WfsBaseType {
        get {
            if (_baseTypes.Count == 0) {
                return null;
            }

            return _baseTypes[0];
        }
    }

    public CodeTypeSyntax? IwfsBaseType {
        get {
            if (_baseTypes.Count < 2) {
                return null;
            }

            return _baseTypes[1];
        }
    }

    // ReSharper disable once InconsistentNaming
    public CodeTypeSyntax? IBeginWfsBaseType {
        get {
            if (_baseTypes.Count < 3) {
                return null;
            }

            return _baseTypes[2];
        }
    }

    public IReadOnlyList<CodeTypeSyntax> BaseTypes => _baseTypes;

}
