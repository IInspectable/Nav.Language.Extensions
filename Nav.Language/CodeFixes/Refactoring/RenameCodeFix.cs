#nullable enable

#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

public abstract class RenameCodeFix: RefactoringCodeFix {

    protected RenameCodeFix(CodeFixContext context)
        : base(context) {
    }

    public abstract string                  ProvideDefaultName();
    public abstract string?                 ValidateSymbolName(string? symbolName);
    public abstract IEnumerable<TextChange> GetTextChanges(string? newName);

}

abstract class RenameCodeFix<T>: RenameCodeFix where T : class, ISymbol {

    protected RenameCodeFix(T symbol, ISymbol originatingSymbol, CodeFixContext context)
        : base(context) {

        Symbol            = symbol            ?? throw new ArgumentNullException(nameof(symbol));
        OriginatingSymbol = originatingSymbol ?? throw new ArgumentNullException(nameof(originatingSymbol));
    }

    protected ISymbol OriginatingSymbol { get; }

    public override TextExtent? ApplicableTo => OriginatingSymbol.Location.Extent;
    public override CodeFixPrio Prio         => CodeFixPrio.Low;

    public T Symbol { get; }

    public override string ProvideDefaultName() {
        return Symbol.Name;
    }

}