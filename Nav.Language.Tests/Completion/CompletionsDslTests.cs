using NUnit.Framework;

using Pharmatechnik.Nav.Language;              // SyntaxFacts
using Pharmatechnik.Nav.Language.Completion;   // NavCompletionItem(Kind)

using static Nav.Language.Tests.Completion.Completions;

namespace Nav.Language.Tests.Completion;

/// <summary>
/// Selbsttests der Fluent-Test-DSL <see cref="Completions"/>. Die reinen Mengen-/Item-Prüfungen
/// bauen ihr <see cref="CompletionResult"/> aus handgemachten Items (deterministisch, entkoppelt vom
/// <see cref="NavCompletionService"/>); der Commit-Effekt wird einmal an echtem Nav und einmal am
/// Host-Default-Bereich verifiziert.
/// </summary>
[TestFixture]
public class CompletionsDslTests {

    // Kleiner Item-Bauhelfer für die synthetischen Ergebnismengen.
    static NavCompletionItem Make(string label, NavCompletionItemKind kind) => new(label, kind);

    static CompletionResult Result(params NavCompletionItem[] items) =>
        new(source: "", caret: 0, items: items);

    #region Offers — exakte Menge (Multiset)

    [Test]
    public void Offers_ExactMatch_Passes() {
        Result(Make("a", NavCompletionItemKind.Keyword),
               Make("Sub", NavCompletionItemKind.Task))
            .Offers(Keyword("a"), Task("Sub"));
    }

    [Test]
    public void Offers_OrderIndependent_Passes() {
        // Andere Reihenfolge als erwartet — .Offers ist mengenbasiert.
        Result(Make("Sub", NavCompletionItemKind.Task),
               Make("a", NavCompletionItemKind.Keyword))
            .Offers(Keyword("a"), Task("Sub"));
    }

    [Test]
    public void Offers_Missing_Fails() {
        Assert.Throws<AssertionException>(() =>
            Result(Make("a", NavCompletionItemKind.Keyword))
                .Offers(Keyword("a"), Keyword("b")));
    }

    [Test]
    public void Offers_Extra_Fails() {
        Assert.Throws<AssertionException>(() =>
            Result(Make("a", NavCompletionItemKind.Keyword),
                   Make("b", NavCompletionItemKind.Keyword))
                .Offers(Keyword("a")));
    }

    [Test]
    public void Offers_WrongKind_Fails() {
        // Richtiges Label, falsche Kind — muss auffallen (behebt die Alt-Entkopplung).
        Assert.Throws<AssertionException>(() =>
            Result(Make("Sub", NavCompletionItemKind.Task))
                .Offers(Keyword("Sub")));
    }

    [Test]
    public void Offers_Duplicate_Fails() {
        // Zwei identische Items, aber nur eines erwartet -> das Multiset meldet das überzählige.
        Assert.Throws<AssertionException>(() =>
            Result(Make("end", NavCompletionItemKind.Keyword),
                   Make("end", NavCompletionItemKind.Keyword))
                .Offers(Keyword("end")));
    }

    [Test]
    public void OffersNothing_Empty_Passes() {
        Result().OffersNothing();
    }

    [Test]
    public void OffersNothing_NonEmpty_Fails() {
        Assert.Throws<AssertionException>(() =>
            Result(Make("a", NavCompletionItemKind.Keyword)).OffersNothing());
    }

    #endregion

    #region Escape-Luken — Includes / Excludes

    [Test]
    public void Includes_PresentSubset_Passes_IgnoringExtras() {
        Result(Make("a", NavCompletionItemKind.Keyword),
               Make("b", NavCompletionItemKind.Keyword),
               Make("c", NavCompletionItemKind.Keyword))
            .Includes(Keyword("a"));   // Extras (b, c) stören nicht.
    }

    [Test]
    public void Includes_MissingOrWrongKind_Fails() {
        Assert.Throws<AssertionException>(() =>
            Result(Make("a", NavCompletionItemKind.Task))
                .Includes(Keyword("a")));   // Label da, aber falsche Kind.
    }

    [Test]
    public void Excludes_Absent_Passes() {
        Result(Make("a", NavCompletionItemKind.Keyword)).Excludes("init", "task");
    }

    [Test]
    public void Excludes_Present_Fails() {
        Assert.Throws<AssertionException>(() =>
            Result(Make("init", NavCompletionItemKind.Keyword)).Excludes("init"));
    }

    #endregion

    #region Item-Drilldown

    [Test]
    public void Item_KindInsertsAndSpanContract() {
        Result(Make("Sub", NavCompletionItemKind.Task))
            .Item("Sub")
            .HasKind(NavCompletionItemKind.Task)
            .Inserts("Sub")
            .LeavesSpanToHost();
    }

    [Test]
    public void Item_HasKind_WrongKind_Fails() {
        Assert.Throws<AssertionException>(() =>
            Result(Make("Sub", NavCompletionItemKind.Task))
                .Item("Sub").HasKind(NavCompletionItemKind.Keyword));
    }

    [Test]
    public void Single_AmbiguousLabel_Throws() {
        // Zwei Items mit gleichem Label -> Single(...) wirft (deckt Duplikate mit ab).
        Assert.Throws<System.InvalidOperationException>(() =>
            Result(Make("x", NavCompletionItemKind.Keyword),
                   Make("x", NavCompletionItemKind.Task))
                .Item("x"));
    }

    #endregion

    #region Commit-Effekt

    [Test]
    public void Commit_WithReplacementExtent_ReplacesOperator() {
        // Echtes Nav: Caret VOR der modalen Edge `o->`; das committete `-->` ersetzt sie
        // (keine Verdopplung) — der Ersetzungsbereich stammt aus dem Item selbst.
        At("""
           task A
           {
               init i;
               exit e;
               i |o-> e;
           }

           """)
            .Commit(SyntaxFacts.GoToEdgeKeyword)
            .Produces("""
                      task A
                      {
                          init i;
                          exit e;
                          i --> e;
                      }

                      """);
    }

    [Test]
    public void Commit_WithoutExtent_UsesHostWordDefault() {
        // Item ohne eigenen Extent -> DefaultWordExtent ersetzt den Bezeichner-Lauf um den Caret.
        // Quelle "task Foo", Caret (Index 5) direkt vor "Foo" -> "Foo" wird durch "Foobar" ersetzt.
        new CompletionResult(source: "task Foo", caret: 5,
                             items: new[] { Make("Foobar", NavCompletionItemKind.Task) })
            .Commit("Foobar")
            .Produces("task Foobar");
    }

    #endregion

}
