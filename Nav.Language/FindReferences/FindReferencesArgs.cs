#nullable enable

#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

public class FindReferencesArgs {

    public FindReferencesArgs(ISymbol originatingSymbol,
                              CodeGenerationUnit originatingCodeGenerationUnit,
                              NavSolution solution,
                              IFindReferencesContext context) {

        OriginatingSymbol             = originatingSymbol             ?? throw new ArgumentNullException(nameof(originatingSymbol));
        OriginatingCodeGenerationUnit = originatingCodeGenerationUnit ?? throw new ArgumentNullException(nameof(originatingCodeGenerationUnit));
        Context                       = context                       ?? throw new ArgumentNullException(nameof(context));
        Solution                      = solution                      ?? throw new ArgumentNullException(nameof(solution));

    }

    public ISymbol OriginatingSymbol { get; }

    public CodeGenerationUnit OriginatingCodeGenerationUnit { get; }

    public NavSolution Solution { get; }

    public IFindReferencesContext Context { get; }

}