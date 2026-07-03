namespace Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;

public abstract class ErrorCodeFix: CodeFix {

    protected ErrorCodeFix(CodeFixContext context): base(context) {
    }

    public sealed override CodeFixCategory Category => CodeFixCategory.ErrorFix;

}