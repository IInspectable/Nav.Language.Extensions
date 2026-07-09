namespace Nav.Language.CodeAnalysis.Tests.Trigger;

// Gemeinsame Fixture für die Trigger-Navigationstests (Nav↔C#). Ein Task mit zwei Views und zwei
// Signal-Triggern (on OnX), damit die Navigation zielgenau die RICHTIGE {Trigger}Logic trifft
// (OnOpen → OnOpenLogic, OnClose → OnCloseLogic) und nicht bloß „irgendeine" Trigger-Methode. Jeder
// on-Trigger wird im generierten Code zu einer {Trigger}Logic-Methode auf der {Task}WFSBase, die die
// konkrete {Task}WFS als Stub überschreibt — das Nav→C#-Sprungziel.
static class TriggerFixtures {

    public const string TriggerFlow =
        """
        #version 2

        [namespaceprefix Nav.Language.CodeAnalysis.Tests.V2.TriggerNav]

        [using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL]
        [using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL]

        task TriggerFlow [base StandardWFS : IWFServiceBase]
            [params]
            [result bool]
        {

            init Start;
            exit Done;
            view Home;
            view Detail;

            Start  --> Home;
            Home   --> Detail on OnOpen;
            Detail --> Done   on OnClose;
        }
        """;
}
