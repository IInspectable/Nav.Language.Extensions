using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

/// <summary>
/// Nav0124 — die generische Member-Kollision der V2-Aufruffläche. Der V2-Codegen erzeugt je Quelle
/// (Init-, Trigger-, Exit-Transition und je Choice-Context) eine <b>flache</b> benannte Aufruffläche:
/// <c>Show{Node}</c> (GUI-Knoten), <c>Begin{Node}</c> (Task-Knoten), der <b>bare-name</b>
/// <c>{Choice}</c>-Forward (Choice-Knoten) sowie die fixen Member <c>Cancel</c>/<c>Exit</c>/<c>End</c>
/// und der genestete Typ <c>Result</c>. Diese <b>eine</b> Diagnose deckt alle Fälle ab, in denen zwei
/// generierte Member kollidieren — anstelle getrennter Analyzer für reservierte Namen und
/// Anzeige-Modus (§4). Ihr Eigenwert ist der <b>still kompilierende Overload</b>, den <c>csc</c> im
/// generierten Code nicht auf die <c>.nav</c>-Stelle rückführbar meldet.
/// </summary>
/// <remarks>
/// Versions-gated: die flache Aufruffläche entsteht erst ab <see cref="NavLanguageVersion.Version2"/>
/// (der V1-Codegen ist <c>switch</c>-basiert und kennt keinen solchen Member-Satz). Die Namensalgebra
/// spiegelt <c>CallContextCodeModel</c>; deshalb liegen die Präfixe hier als lokale Konstanten.
/// </remarks>
public class Nav0124GeneratedMember0CollidesWithAnotherMember: NavAnalyzer {

    /// <inheritdoc/>
    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0124GeneratedMember0CollidesWithAnotherMember;

    // Namenspräfixe der V2-Callables (mode-freies Show-Verb bzw. Begin-Factory) — siehe CallContextCodeModel.
    const string ShowPrefix  = "Show";
    const string BeginPrefix = "Begin";

    // Framework-fixe bzw. immer vorhandene Member jeder Aufruffläche: das genestete Result und die
    // Kommandos Cancel/Exit/End. Ein bare-name Choice-Forward {Choice} darf mit keinem kollidieren.
    static readonly ImmutableHashSet<string> ReservedMemberNames =
        ImmutableHashSet.Create(StringComparer.Ordinal, "Cancel", "Exit", "End", "Result");

    /// <inheritdoc/>
    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {

        // Die V2-Aufruffläche (und damit jede Kollision) entsteht erst ab Version 2 — unter #version 1
        // gibt es keinen flachen Member-Satz. NavLanguageVersion.Version2 ist die eine Autorität für die
        // Version, ab der die V2-Contexte entstehen (§4).
        if ((taskDefinition.CodeGenerationUnit?.LanguageVersion ?? NavLanguageVersion.Default) < NavLanguageVersion.Version2) {
            yield break;
        }

        // Dieselbe Kollision kann über mehrere Quellen an dieselbe .nav-Stelle verankert werden (z.B. eine
        // Choice namens Cancel, die von Init UND Trigger geforwardet wird) — je (Member, Ort) nur einmal melden.
        var reported = new HashSet<(string Member, int Start)>();

        foreach (var surface in MemberSurfaces(taskDefinition)) {
            foreach (var collision in AnalyzeSurface(surface)) {
                if (reported.Add((collision.MemberName, collision.Primary.Start))) {
                    yield return collision.ToDiagnostic(Descriptor);
                }
            }
        }
    }

    /// <summary>
    /// Die Member-Flächen (Kanten-Quellen), aus denen der V2-Codegen je einen Call-Context baut:
    /// nicht-abstrakte Init-Knoten, jede Trigger-Transition, erreichbare
    /// nicht-<c>[notimplemented]</c>/nicht-abstrakte Task-Knoten (Exit) sowie erreichbare Choices.
    /// Init/Trigger/Exit entsprechen der Quellen-Auswahl von <c>CodeModelBuilderV2</c> (abstrakte
    /// Quellen bekommen dort keinen Call-Context); die Choice-Erreichbarkeit wird hier über
    /// <see cref="INodeSymbol.IsReachable"/> bestimmt.
    /// </summary>
    static IEnumerable<IReadOnlyList<IEdge>> MemberSurfaces(ITaskDefinitionSymbol taskDefinition) {

        foreach (var initNode in taskDefinition.NodeDeclarations.OfType<IInitNodeSymbol>()) {
            if (!initNode.CodeGenerateAbstractMethod()) {
                yield return initNode.Outgoings.Cast<IEdge>().ToList();
            }
        }

        foreach (var triggerTransition in taskDefinition.TriggerTransitions) {
            yield return new IEdge[] { triggerTransition };
        }

        foreach (var taskNode in taskDefinition.NodeDeclarations.OfType<ITaskNodeSymbol>()) {
            if (taskNode.IsReachable() && !taskNode.CodeNotImplemented() && !taskNode.CodeGenerateAbstractMethod()) {
                yield return taskNode.Outgoings.Cast<IEdge>().ToList();
            }
        }

        foreach (var choiceNode in taskDefinition.NodeDeclarations.OfType<IChoiceNodeSymbol>()) {
            if (choiceNode.IsReachable()) {
                yield return choiceNode.Outgoings.Cast<IEdge>().ToList();
            }
        }
    }

    /// <summary>Die Kollisionen einer einzelnen Aufruffläche — Name-Kollision (bare-name Choice) und Anzeige-Modus.</summary>
    static IEnumerable<Collision> AnalyzeSurface(IReadOnlyList<IEdge> surface) {

        // Wie GetDirectCalls: nur aufgelöste Ziele mit definiertem Kantenmodus tragen einen Member;
        // unaufgelöste Knoten bleiben Nav0011 überlassen (Roslyn-Stil: eine treffende Diagnose).
        var resolved = surface.Where(edge => edge.EdgeMode != null && edge.TargetReference?.Declaration != null)
                              .ToList();

        var targets = resolved.Select(edge => edge.TargetReference!.Declaration!)
                              .Distinct()
                              .ToList();

        // --- Name-Kollision: der bare-name {Choice}-Forward gegen den übrigen Member-Satz ---------------
        // Die „Landschaft" der übrigen Member: reservierte Namen + Show{Gui}/Begin{Task}. Ein Choice-Forward
        // trägt als einziger keinen Verb-Präfix und kann daher als einziger mit ihnen kollidieren.
        var existing = new Dictionary<string, Location?>(StringComparer.Ordinal);
        foreach (var reserved in ReservedMemberNames) {
            existing[reserved] = null;
        }

        foreach (var node in targets) {
            switch (node) {
                case IGuiNodeSymbol:
                    existing[$"{ShowPrefix}{node.Name.ToPascalcase()}"] = node.Location;
                    break;
                case ITaskNodeSymbol:
                    existing[$"{BeginPrefix}{node.Name.ToPascalcase()}"] = node.Location;
                    break;
            }
        }

        foreach (var choice in targets.OfType<IChoiceNodeSymbol>()) {
            var member = choice.Name.ToPascalcase();
            if (existing.TryGetValue(member, out var other)) {
                yield return new Collision(member, choice.Location, other);
            } else {
                // Eine weitere Choice gleichen Namens würde ihrerseits kollidieren.
                existing[member] = choice.Location;
            }
        }

        // --- Anzeige-Modus-Kollision: EIN Ziel, mehrere plain-Kanten mit verschiedenem Modus -------------
        // Zwei Kanten zum selben GUI-/Task-Ziel bei unterschiedlichem Anzeige-Modus (goto/modal/nonmodal),
        // beide OHNE Continuation, erzeugen dieselbe Show{Node}/Begin{Node}-Signatur — nicht über den
        // Rückgabetyp lösbar (anders als die plain+Continuation-Union, §3.4).
        var plainByTarget = resolved
                           .Where(edge => edge.TargetReference!.Declaration is IGuiNodeSymbol or ITaskNodeSymbol)
                           .Where(edge => (edge as IContinuableEdge)?.ContinuationTransition == null)
                           .GroupBy(edge => edge.TargetReference!.Declaration!);

        foreach (var group in plainByTarget) {

            var distinctModes = group.Select(edge => edge.EdgeMode!.EdgeMode).Distinct().ToList();
            if (distinctModes.Count <= 1) {
                continue;
            }

            var node   = group.Key;
            var prefix = node is IGuiNodeSymbol ? ShowPrefix : BeginPrefix;
            var member = $"{prefix}{node.Name.ToPascalcase()}";

            var locations = group.Select(edge => edge.EdgeMode!.Location).ToList();
            yield return new Collision(member, locations[0], locations.Skip(1).ToList());
        }
    }

    /// <summary>Eine erkannte Member-Kollision: der kollidierende Member-Name und die verankerten Orte.</summary>
    readonly struct Collision {

        public Collision(string memberName, Location primary, Location? additional)
            : this(memberName, primary, additional == null ? Array.Empty<Location>() : new[] { additional }) {
        }

        public Collision(string memberName, Location primary, IReadOnlyList<Location> related) {
            MemberName = memberName;
            Primary    = primary;
            Related    = related;
        }

        public string                  MemberName { get; }
        public Location                 Primary    { get; }
        public IReadOnlyList<Location>  Related    { get; }

        public Diagnostic ToDiagnostic(DiagnosticDescriptor descriptor) {
            return new Diagnostic(Primary, Related, descriptor, MemberName);
        }

    }

}
