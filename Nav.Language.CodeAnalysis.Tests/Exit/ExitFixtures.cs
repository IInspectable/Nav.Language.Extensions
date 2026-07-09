namespace Nav.Language.CodeAnalysis.Tests.Exit;

// Gemeinsame Fixture für die Exit-Navigationstests (Nav↔C#). Ein Sub-Task Sub mit ZWEI Exit-Punkten
// (E1, E2), im Parent modal geöffnet und beide Exits geroutet. Besonderheit des Exit-Konstrukts: die
// generierte Rücksprung-Logik existiert pro Task-KNOTEN (eine After{TaskNode}Logic für Sub), nicht pro
// Exit-Punkt. Der Nav→C#-Sprung (E-Punkt → AfterSubLogic) ist daher eindeutig; der C#→Nav-Rücksprung
// (AfterSubLogic → E-Punkte) ist MEHRDEUTIG und liefert je Exit-Punkt eine AmbiguousLocation.
// Sub bleibt bewusst ein taskref: die AfterSubLogic entsteht im PARENT (ExitFlowWFS), nicht in Sub.
static class ExitFixtures {

    public const string ExitFlow =
        """
        #version 2

        [namespaceprefix Nav.Language.CodeAnalysis.Tests.V2.ExitNav]

        [using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL]
        [using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL]

        taskref Sub [namespaceprefix NS.V2.ExitNav]
                    [result SubResult r] {

            init;
            exit E1;
            exit E2;
        }

        task ExitFlow [base StandardWFS : IWFServiceBase]
            [params]
            [result bool]
        {

            init Start;
            exit Done;
            task Sub;
            view Home;

            Start  --> Home;
            Home   o-> Sub on OnStart;
            Sub:E1 --> Home;
            Sub:E2 --> Done;
        }
        """;
}
