using System;
using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

namespace Nav.Language.Tests;

/// <summary>
/// Stellt sicher, dass der Tiefendurchlauf des generierten <see cref="SyntaxNodeWalker"/> jeden konkreten
/// <see cref="SyntaxNode"/>-Typ erreicht. Geprüft wird der Walker-Pfad selbst (<c>Walk</c> ⇒
/// <c>ChildNodes()</c> ⇒ <c>WalkXxx</c>) — die reine Sample-Abdeckung von <c>AllRules.nav</c> deckt
/// daneben <see cref="SyntaxTreeTests.TestAllSyntaxesPresent"/> über einen anderen Durchlauf ab.
/// </summary>
[TestFixture]
public class SyntaxWalkerTests {

    static IEnumerable<Type> ConcreteNodeTypes => typeof(SyntaxNode).Assembly
                                                                    .GetTypes()
                                                                    .Where(t => typeof(SyntaxNode).IsAssignableFrom(t) && !t.IsAbstract)
                                                                    .OrderBy(t => t.Name, StringComparer.Ordinal);

    // Einmalig den kompletten Korpus durchlaufen und merken, welche Knotentypen besucht wurden.
    static readonly IReadOnlyCollection<Type> WalkedNodeTypes = WalkAllRules();

    static IReadOnlyCollection<Type> WalkAllRules() {
        var walker = new RecordingWalker();

        var tree = SyntaxTree.ParseText(Resources.AllRules);
        walker.Walk(tree.Root);

        // Direktiven und Skip-Läufe sind strukturierte Trivia (keine Kindknoten der Wurzel) — separat
        // einspeisen, damit auch ihre generierten WalkXxx-Methoden erreicht werden. AllRules trägt die
        // wirksame #version (VersionDirectiveSyntax); eine unbekannte Direktive (BadDirectiveTriviaSyntax)
        // und ein Skip-Lauf (SkippedTokensTriviaSyntax) aus eigenen Schnipseln, da AllRules bewusst
        // fehlerfrei bleibt.
        foreach (var directive in tree.Directives()) {
            walker.Walk(directive);
        }

        foreach (var directive in SyntaxTree.ParseText(
                     """
                     #unknown
                     task A{}
                     """).Directives()) {
            walker.Walk(directive);
        }

        foreach (var skipped in SyntaxTree.ParseText(
                     """
                     task A
                     {
                         init [];
                     }
                     """).SkippedTokens()) {
            walker.Walk(skipped);
        }

        // Die Continuation-Konstrukte und das cancel-Kantenziel (ab Sprachversion 2) fehlen in AllRules
        // (Version 1) — eigener Schnipsel, damit auch ihre generierten WalkXxx-Methoden erreicht werden.
        walker.Walk(SyntaxTree.ParseText(
                        """
                        #version 2
                        task A
                        {
                            view V;
                            task T;
                            V --> V o-^ T;
                            V --> V --^ T;
                            V --> cancel;
                        }
                        """).Root);

        return walker.Walked;
    }

    [TestCaseSource(nameof(ConcreteNodeTypes))]
    public void WalkReachesEveryNodeType(Type nodeType) {
        Assert.That(WalkedNodeTypes, Does.Contain(nodeType),
                    $"{nodeType.Name} wurde beim Walk über AllRules.nav nicht erreicht.");
    }

    /// <summary>
    /// Walker, der über die eine <see cref="SyntaxNodeWalker.DefaultWalk"/>-Überschreibung jeden besuchten
    /// Knotentyp protokolliert — jede generierte <c>WalkXxx</c>-Methode führt darauf zurück.
    /// </summary>
    sealed class RecordingWalker: SyntaxNodeWalker {

        public readonly HashSet<Type> Walked = new();

        public override bool DefaultWalk(SyntaxNode node) {
            Walked.Add(node.GetType());
            return true;
        }

    }

}
