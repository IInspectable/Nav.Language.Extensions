#nullable enable

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring;

public abstract class RefactoringCodeFix: CodeFix {

    protected RefactoringCodeFix(CodeFixContext context): base(context) {
    }

    public sealed override CodeFixCategory Category => CodeFixCategory.Refactoring;

}