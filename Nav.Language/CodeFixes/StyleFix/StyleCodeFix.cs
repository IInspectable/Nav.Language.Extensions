#nullable enable

using Pharmatechnik.Nav.Language.Text;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix;

public abstract class StyleCodeFix: CodeFix {

    protected StyleCodeFix(CodeFixContext context): base(context) {
    }

    public sealed override CodeFixCategory Category => CodeFixCategory.StyleFix;

    public abstract IList<TextChange> GetTextChanges();

}