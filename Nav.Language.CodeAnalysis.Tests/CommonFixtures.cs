namespace Nav.Language.CodeAnalysis.Tests;

/// <summary>
/// Konstrukt-neutrale Fixtures, die von mehreren Navigationstest-Ordnern geteilt werden — Gegenstück zum
/// feature-neutralen Harness (<see cref="GoldenAssert"/>, <see cref="NavigationSnapshot"/>, …) in der
/// Projektwurzel. Konstrukt-spezifische Fixtures bleiben dagegen in ihrem jeweiligen Ordner
/// (<c>Choice/ChoiceFixtures.cs</c> usw.).
/// </summary>
static class CommonFixtures {

    // Ein minimaler, von den Feature-Fixtures völlig unabhängiger Task (eigener {Task}WFSBase) für die
    // Negativpfade: eine Roslyn-Bühne, in der die jeweils gesuchte WFSBase bewusst FEHLT — damit die
    // Ziel-Auflösung des LocationFinder ins Leere läuft und als LocationNotFoundException gemeldet werden
    // muss. Genutzt über CodeAnalysisTestContext.ForeignProject().
    public const string UnrelatedFlow =
        """
        #version 2

        [namespaceprefix NS.Unrelated]

        task Unrelated [base StandardWFS : IWFServiceBase]
            [params]
            [result bool]
        {

            init Start;
            exit Done;
            view Home;

            Start --> Home;
            Home  --> Done on OnClose;
        }
        """;
}
