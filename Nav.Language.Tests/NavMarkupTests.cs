using System;

using NUnit.Framework;

namespace Nav.Language.Tests;

[TestFixture]
public class NavMarkupTests {

    [Test]
    public void Caret_IsStripped_AndPositionReported() {

        var m = NavMarkup.Parse("task |Sub;");

        Assert.That(m.Source, Is.EqualTo("task Sub;"));
        Assert.That(m.HasCaret, Is.True);
        Assert.That(m.Caret, Is.EqualTo("task ".Length));
        // Der Caret sitzt genau vor dem Knotennamen.
        Assert.That(m.Source.Substring(m.Caret, 3), Is.EqualTo("Sub"));
    }

    [Test]
    public void Caret_Absent_YieldsMinusOne() {

        var m = NavMarkup.Parse("task Sub;");

        Assert.That(m.HasCaret, Is.False);
        Assert.That(m.Caret, Is.EqualTo(-1));
    }

    [Test]
    public void Caret_MoreThanOne_Throws() {
        Assert.That(() => NavMarkup.Parse("a|b|c"), Throws.TypeOf<FormatException>());
    }

    [Test]
    public void NamedSpan_CoversContent() {

        var m = NavMarkup.Parse("exit |decl:e1|;");

        Assert.That(m.Source, Is.EqualTo("exit e1;"));
        var decl = m.Span("decl");
        Assert.That(decl.Start, Is.EqualTo("exit ".Length));
        Assert.That(decl.Length, Is.EqualTo(2));
        Assert.That(m.Source.Substring(decl.Start, decl.Length), Is.EqualTo("e1"));
        Assert.That(m.Position("decl"), Is.EqualTo(decl.Start));
    }

    [Test]
    public void AnonymousSpan_CoversContent() {

        var m = NavMarkup.Parse("exit |:e1|;");

        Assert.That(m.Source, Is.EqualTo("exit e1;"));
        Assert.That(m.Source.Substring(m.AnonymousSpan.Start, m.AnonymousSpan.Length), Is.EqualTo("e1"));
    }

    [Test]
    public void NamedPosition_EmptyContent_HasLengthZero() {

        var m = NavMarkup.Parse("exit |decl:|e1;");

        Assert.That(m.Source, Is.EqualTo("exit e1;"));
        Assert.That(m.Span("decl").Length, Is.EqualTo(0));
        // Länge 0 ⇒ benannte Position genau vor 'e1'.
        Assert.That(m.Position("decl"), Is.EqualTo("exit ".Length));
    }

    [Test]
    public void RepeatedName_CollectsAllOccurrences() {

        var m = NavMarkup.Parse(
            """
            task A
            {
                init I1;
                exit |hit:e1|;

                I1 --> |hit:e1|;
            }
            """);

        var hits = m.Spans("hit");
        Assert.That(hits, Has.Length.EqualTo(2));
        foreach (var hit in hits) {
            Assert.That(m.Source.Substring(hit.Start, hit.Length), Is.EqualTo("e1"));
        }
    }

    [Test]
    public void BarePipePair_IsTwoCarets_NotAnonymousSpan_Throws() {
        // '|a|' ist KEIN Span, sondern zwei Carets um 'a' — deshalb wirft es (mehr als ein Caret).
        // Ein anonymer Span muss '|:a|' geschrieben werden. Dieser Test hält die Design-Regel fest.
        Assert.That(() => NavMarkup.Parse("|a|"), Throws.TypeOf<FormatException>());
    }

    [Test]
    public void Escape_ProducesLiteralPipe() {

        var m = NavMarkup.Parse("a||b");

        Assert.That(m.Source, Is.EqualTo("a|b"));
        Assert.That(m.HasCaret, Is.False);
    }

    [Test]
    public void UnbalancedSpan_Throws() {
        Assert.That(() => NavMarkup.Parse("exit |decl:e1;"), Throws.TypeOf<FormatException>());
    }

    [Test]
    public void Multiline_RawString_StripsAllMarkers_AndMapsPositions() {

        var m = NavMarkup.Parse(
            """
            task A
            {
                init I1;
                exit |decl:e1|;

                I1 --> |ref:e1|;
            }
            """);

        // Alle Marker sind aus dem Source verschwunden.
        Assert.That(m.Source, Does.Not.Contain("|"));
        // Deklaration und Referenz zeigen jeweils auf 'e1' im bereinigten Source.
        Assert.That(m.Source.Substring(m.Position("decl"), 2), Is.EqualTo("e1"));
        Assert.That(m.Source.Substring(m.Position("ref"),  2), Is.EqualTo("e1"));
        // Es sind verschiedene Vorkommen.
        Assert.That(m.Position("decl"), Is.Not.EqualTo(m.Position("ref")));
    }

}
