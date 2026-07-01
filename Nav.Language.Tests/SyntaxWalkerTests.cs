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

        // Direktiven sind strukturierte Trivia (keine Kindknoten der Wurzel) — separat einspeisen, damit auch
        // ihre generierten WalkXxx-Methoden erreicht werden. AllRules trägt die wirksame #pragma version
        // (VersionDirectiveSyntax); eine unbekannte Direktive (BadDirectiveTriviaSyntax) aus einem eigenen
        // Schnipsel, da AllRules bewusst fehlerfrei bleibt.
        foreach (var directive in tree.Directives()) {
            walker.Walk(directive);
        }

        foreach (var directive in SyntaxTree.ParseText("#unknown\r\ntask A{}").Directives()) {
            walker.Walk(directive);
        }

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
