namespace Nav.Language.CodeAnalysis.Tests.Task;

// Gemeinsame Fixture für die Task-Navigationstests (Nav↔C#). Zwei vollständige Task-Definitionen im
// selben File, damit die Navigation zielgenau die RICHTIGE konkrete {Task}WFS trifft (TaskFlow → TaskFlowWFS,
// Helper → HelperWFS) und nicht bloß „irgendeine" WFS-Klasse. Jede task-Definition erzeugt eine eigene
// {Task}WFSBase (generiert) + konkrete {Task}WFS (Override-Stubs) — Letztere ist das Nav→C#-Sprungziel.
static class TaskFixtures {

    public const string TaskFlow =
        """
        #version 2

        [namespaceprefix Nav.Language.CodeAnalysis.Tests.V2.TaskNav]

        [using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL]
        [using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL]

        task TaskFlow [base StandardWFS : IWFServiceBase]
            [params]
            [result bool]
        {

            init Start;
            exit Done;
            view Home;

            Start --> Home;
            Home  --> Done on OnClose;
        }

        task Helper [base StandardWFS : IWFServiceBase]
            [params]
            [result bool]
        {

            init Begin;
            exit End;
            view Panel;

            Begin --> Panel;
            Panel --> End on OnFinish;
        }
        """;
}
