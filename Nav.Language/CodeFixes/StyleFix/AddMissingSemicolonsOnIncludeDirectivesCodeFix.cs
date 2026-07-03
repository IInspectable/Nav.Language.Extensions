#region Using Directives

using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix; 

public class AddMissingSemicolonsOnIncludeDirectivesCodeFix: StyleCodeFix {

    internal AddMissingSemicolonsOnIncludeDirectivesCodeFix(CodeFixContext context)
        : base(context) {
    }

    public override string        Name         => "Add missing ';' on Include Directives";
    public override CodeFixImpact Impact       => CodeFixImpact.None;
    public override TextExtent?   ApplicableTo => null;
    public override CodeFixPrio   Prio         => CodeFixPrio.Low;

    internal bool CanApplyFix() {
        return GetCanditates().Any();
    }

    IEnumerable<IncludeDirectiveSyntax> GetCanditates() {
        return Syntax.DescendantNodes<IncludeDirectiveSyntax>().Where(ids => ids.Semicolon.IsMissing);
    }

    public override IList<TextChange> GetTextChanges() {

        var textChanges = new List<TextChange>();

        foreach (var includeDirectiveSyntax in GetCanditates()) {
            textChanges.AddRange(GetInsertChanges(includeDirectiveSyntax.End, SyntaxFacts.Semicolon.ToString()));
        }

        return textChanges;
    }

}