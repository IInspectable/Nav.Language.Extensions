namespace Nav.Language.CodeAnalysis.Tests.Choice;

// Gemeinsame Fixture für die Choice-Navigationstests (Nav↔C#, FindReferences). Entspricht dem
// Golden-Input `Regression/Tests/V2/ChoiceFlow.nav`: drei Quellen (Init/Trigger/Exit) delegieren an
// dieselbe Choice_Retry; Choice_Retry forwardet weiter an Choice_Escalate (Choice→Choice). Genau der
// durchgängige Fall, der bei der Einführung der V2-Choice-Navigation mehrfach nachgebessert werden musste.
static class ChoiceFixtures {

    public const string ChoiceFlow =
        """
        #version 2

        [namespaceprefix Nav.Language.CodeAnalysis.Tests.V2.Choice]

        [using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL]
        [using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL]

        taskref A [namespaceprefix NS.V2.Choice]
                  [result AResult r1] {

            init;
            exit E1;
            exit E2;
        }

        taskref Msg [namespaceprefix NS.V2.Choice]
                    [result MsgResult r] {

            init [params string text];
            exit Ok;
        }

        task ChoiceFlow [base StandardWFS : IWFServiceBase]
            [params]
            [result bool]
        {

            init Init1 [params string message];
            exit Done;
            exit Esc;
            task A;
            task Msg;
            view Home;
            choice Choice_Retry    [params string reason];
            choice Choice_Escalate [params int level];

            // Drei Quellen delegieren an dieselbe Choice:
            Init1 --> Choice_Retry;                // Quelle 1: Init-Transition
            Home  --> Choice_Retry on OnRetry;     // Quelle 2: Trigger-Transition
            A:E1  --> Choice_Retry;                // Quelle 3: Exit-Transition

            Home  o-> A on OnStartA;
            A:E2  --> Home;

            // Choice-Ausgänge:
            Choice_Retry --> Home;                        // plain      ┐ Union auf Home
            Choice_Retry --> Home o-^ Msg if "Fehler";    // Continuation┘
            Choice_Retry --> Choice_Escalate if "Fatal";  // Choice→Choice

            Choice_Escalate --> Done;                      // Multi-Exit-Ziel 1
            Choice_Escalate --> Esc;                       // Multi-Exit-Ziel 2

            Msg:Ok --> Home;
        }
        """;

    // Nutzerseitiger Logic-Code, der die Choice-Forwards aufruft. Erst durch solche Aufrufe entsteht
    // überhaupt eine Stelle, an der die <NavChoiceCall>-Annotation greift (die generierten Override-Stubs
    // rufen die Forwards nicht auf). Namespace = {namespaceprefix}.WFL des Fixtures.
    //   • next.Choice_Retry(…) bewusst an ZWEI Aufrufstellen (getrennte Methoden), damit die
    //     „{Choice}Logic → Aufrufer"-Navigation mehrere Treffer klassenweit findet.
    //   • next.Choice_Escalate(…) im Choice_RetryCallContext — der Choice→Choice-Sprung
    //     (Choice_Retry --> Choice_Escalate), der historisch fragile Fall. Der CallContext-Typ ist ein
    //     protected-verschachtelter Typ der {Task}WFSBase, aus der abgeleiteten WFS unqualifiziert erreichbar.
    public const string ChoiceFlowUserCode =
        """
        namespace Nav.Language.CodeAnalysis.Tests.V2.Choice.WFL {
            partial class ChoiceFlowWFS {
                void CallForwards(Init1CallContext next) {
                    next.Choice_Retry("warn");
                }
                void CallForwardsAgain(Init1CallContext next) {
                    next.Choice_Retry("retry");
                }
                void CallEscalateFromRetry(Choice_RetryCallContext next) {
                    next.Choice_Escalate(1);
                }
            }
        }
        """;

    // Minimaler, von ChoiceFlow unabhängiger Task (anderer {Task}WFSBase) für den Negativpfad: eine
    // Roslyn-Bühne, in der die ChoiceFlow-WFSBase bewusst FEHLT — damit die Ziel-Auflösung des
    // LocationFinder ins Leere läuft und als LocationNotFoundException gemeldet werden muss.
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
