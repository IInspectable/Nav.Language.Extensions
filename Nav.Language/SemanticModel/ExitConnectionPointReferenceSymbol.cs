using JetBrains.Annotations;

namespace Pharmatechnik.Nav.Language; 

sealed partial class ExitConnectionPointReferenceSymbol: Symbol, IExitConnectionPointReferenceSymbol {

    // ReSharper disable once NotNullMemberIsNotInitialized ExitTransition wird definitiv im Ctor der ExitTransition gesetzt
    public ExitConnectionPointReferenceSymbol(SyntaxTree syntaxTree, string name, [NotNull] Location location, IExitConnectionPointSymbol connectionPoint)
        : base(name, location) {
        SyntaxTree  = syntaxTree;
        Declaration = connectionPoint;
    }

    public override SyntaxTree SyntaxTree { get; }

    public IExitConnectionPointSymbol Declaration    { get; }
    public IExitTransition            ExitTransition { get; internal set; }

}