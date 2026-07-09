namespace Nav.Language.CodeAnalysis.Tests.Init;

// Gemeinsame Fixture für die Init-Navigationstests (Nav↔C#, Knoten- UND Call-Pfad). Zwei Positionen:
//   • der EIGENE Init-Knoten des Tasks (init Init1) → {Begin}Logic der eigenen WFS (Nav→C#),
//   • die Aufrufstelle next.BeginChild() eines geöffneten SUB-Tasks → dessen {Child}-BeginLogic (C#→C#).
// Damit der Call-Pfad auflöst, muss der aufgerufene Task LOKAL definiert sein (dann werden IBeginChildWFS
// + ChildWFS + ChildWFS.BeginLogic in derselben Kompilation erzeugt) — anders als bei den taskref-Fällen
// (A/Msg im ChoiceFlow), deren Interfaces erst im Mehrdatei-Build entstünden. Der Kind-Task Child wird
// über den GUI-Knoten Home modal geöffnet (Home o-> Child on OnStart) → generierter Begin-Wrapper
// BeginChild() mit <NavInitCall>…IBeginChildWFS.
static class InitFixtures {

    public const string InitFlow =
        """
        #version 2

        [namespaceprefix Nav.Language.CodeAnalysis.Tests.V2.InitNav]

        [using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL]
        [using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL]

        task Child [base StandardWFS : IWFServiceBase]
            [params]
            [result bool]
        {

            init Begin;
            exit Ok;
            view Panel;

            Begin --> Panel;
            Panel --> Ok on OnDone;
        }

        task InitFlow [base StandardWFS : IWFServiceBase]
            [params]
            [result bool]
        {

            init Init1 [params string message];
            exit Done;
            task Child;
            view Home;

            Init1    --> Home;
            Home     o-> Child on OnStart;
            Child:Ok --> Home;
            Home     --> Done on OnClose;
        }
        """;

    // Nutzerseitiger Logic-Code, der den generierten Begin-Wrapper des Sub-Tasks aufruft. Erst durch den
    // Aufruf next.BeginChild() entsteht die Stelle, an der die <NavInitCall>-Annotation greift (die
    // generierten Override-Stubs rufen den Wrapper nicht auf). Der Wrapper sitzt im OnStartCallContext des
    // öffnenden Triggers (Home o-> Child on OnStart) — aus der abgeleiteten WFS unqualifiziert erreichbar.
    public const string InitFlowUserCode =
        """
        namespace Nav.Language.CodeAnalysis.Tests.V2.InitNav.WFL {
            partial class InitFlowWFS {
                void CallBeginChild(OnStartCallContext next) {
                    next.BeginChild();
                }
            }
        }
        """;
}
