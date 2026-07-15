#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

/// <summary>
/// Die Anfrage einer Referenzsuche — bündelt das Symbol, von dem aus gesucht wird
/// (<see cref="OriginatingSymbol"/>), dessen Herkunftsdatei (<see cref="OriginatingCodeGenerationUnit"/>),
/// die zu durchsuchende <see cref="Solution"/> und den <see cref="Context"/>, an den Fortschritt und
/// Ergebnisse gemeldet werden. Wird an <see cref="ReferenceFinder.FindReferencesAsync"/> übergeben.
/// </summary>
public class FindReferencesArgs {

    /// <summary>
    /// Erzeugt eine Suchanfrage.
    /// </summary>
    /// <param name="originatingSymbol">Das Symbol, dessen Referenzen gesucht werden.</param>
    /// <param name="originatingCodeGenerationUnit">Die Datei, in der das Ausgangssymbol steht.</param>
    /// <param name="solution">Die zu durchsuchende Solution.</param>
    /// <param name="context">Die Senke für Fortschritt und Ergebnisse.</param>
    /// <exception cref="ArgumentNullException">Ein Argument ist <c>null</c>.</exception>
    public FindReferencesArgs(ISymbol originatingSymbol,
                              CodeGenerationUnit originatingCodeGenerationUnit,
                              NavSolution solution,
                              IFindReferencesContext context) {

        OriginatingSymbol             = originatingSymbol             ?? throw new ArgumentNullException(nameof(originatingSymbol));
        OriginatingCodeGenerationUnit = originatingCodeGenerationUnit ?? throw new ArgumentNullException(nameof(originatingCodeGenerationUnit));
        Context                       = context                       ?? throw new ArgumentNullException(nameof(context));
        Solution                      = solution                      ?? throw new ArgumentNullException(nameof(solution));

    }

    /// <summary>Das Symbol, dessen Referenzen gesucht werden.</summary>
    public ISymbol OriginatingSymbol { get; }

    /// <summary>Die Datei (Codegenerierungs-Einheit), in der das <see cref="OriginatingSymbol"/> steht.</summary>
    public CodeGenerationUnit OriginatingCodeGenerationUnit { get; }

    /// <summary>Die zu durchsuchende Solution.</summary>
    public NavSolution Solution { get; }

    /// <summary>Die Senke, an die Fortschritt und Ergebnisse gemeldet werden.</summary>
    public IFindReferencesContext Context { get; }

}