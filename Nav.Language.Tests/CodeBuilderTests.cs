#region Using Directives

using System;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Nav.Language.Tests;

[TestFixture]
public class CodeBuilderTests {

    static CodeBuilder NewBuilder() => new();

    /// <summary>
    /// Struktureller Golden-Vergleich: normalisiert die Zeilenenden auf beiden Seiten, damit die
    /// Raw-String-Erwartungen unabhängig vom Zeilenende der Quelldatei sind (Raw-String-Literale
    /// übernehmen es). Dass der Builder standardmäßig CRLF schreibt, sichern die dedizierten
    /// Zeilenumbruch-Tests (mit explizitem <c>\r\n</c>) separat ab.
    /// </summary>
    static void AssertCode(CodeBuilder builder, string expected) {
        static string Normalize(string value) => value.Replace("\r\n", "\n");

        Assert.That(Normalize(builder.ToString()), Is.EqualTo(Normalize(expected)));
    }

    // -- Zeilenumbruch-/Whitespace-Mechanik (exakt, mit explizitem \r\n) ------------------------------

    [Test]
    public void EmptyBuilder_YieldsEmptyString() {
        Assert.That(NewBuilder().ToString(), Is.EqualTo(""));
    }

    [Test]
    public void Write_AppendsVerbatim_WithoutNewline() {
        var b = NewBuilder();
        b.Write("foo").Write("bar");
        Assert.That(b.ToString(), Is.EqualTo("foobar"));
    }

    [Test]
    public void WriteLine_AppendsCrLf() {
        var b = NewBuilder();
        b.WriteLine("foo");
        Assert.That(b.ToString(), Is.EqualTo("foo\r\n"));
    }

    [Test]
    public void NewLine_DefaultsToCrLf() {
        Assert.That(NewBuilder().NewLine, Is.EqualTo("\r\n"));
    }

    [Test]
    public void NewLine_ReflectsConfiguredValue() {
        Assert.That(new CodeBuilder(newLine: "\n").NewLine, Is.EqualTo("\n"));
    }

    [Test]
    public void WriteLine_Empty_ProducesBlankLine_WithoutIndent() {
        var b = NewBuilder();
        using (b.Indent()) {
            b.WriteLine();          // Leerzeile mitten in eingerücktem Block
            b.WriteLine("x");
        }

        // Führende Leerzeile ohne Einzug, danach die eingerückte Zeile.
        Assert.That(b.ToString(), Is.EqualTo("\r\n    x\r\n"));
    }

    [Test]
    public void Write_LoneLineFeed_TreatedAsCrLf() {
        var b = NewBuilder();
        b.Write("a\nb");
        Assert.That(b.ToString(), Is.EqualTo("a\r\nb"));
    }

    [Test]
    public void TrailingWhitespace_IsDropped() {
        var b = NewBuilder();
        b.Write("value;   ").WriteLine();
        b.WriteLine("next");
        Assert.That(b.ToString(), Is.EqualTo("value;\r\nnext\r\n"));
    }

    [Test]
    public void InteriorWhitespace_IsPreserved() {
        var b = NewBuilder();
        b.WriteLine("a   b");
        Assert.That(b.ToString(), Is.EqualTo("a   b\r\n"));
    }

    // -- Einrückung & Blöcke (struktureller Golden-Vergleich) ----------------------------------------

    [Test]
    public void Indent_AppliesFourSpaces_PerLevel() {
        var b = NewBuilder();
        b.WriteLine("a");
        using (b.Indent()) {
            b.WriteLine("b");
            using (b.Indent()) {
                b.WriteLine("c");
            }

            b.WriteLine("d");
        }

        b.WriteLine("e");

        AssertCode(b,
                   """
                   a
                       b
                           c
                       d
                   e

                   """);
    }

    [Test]
    public void Indent_Scope_RestoresPreviousDepth_OnDispose() {
        var b = NewBuilder();
        Assert.That(b.IndentDepth, Is.EqualTo(0));
        using (b.Indent()) {
            Assert.That(b.IndentDepth, Is.EqualTo(1));
        }

        Assert.That(b.IndentDepth, Is.EqualTo(0));
    }

    [Test]
    public void PopIndent_BelowZero_Throws() {
        Assert.That(() => NewBuilder().PopIndent(), Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void Block_WritesBraces_AndIndentsBody() {
        var b = NewBuilder();
        b.Write("namespace Foo ");
        using (b.Block()) {
            b.WriteLine("int x;");
        }

        AssertCode(b,
                   """
                   namespace Foo {
                       int x;
                   }
                   """);
    }

    [Test]
    public void Block_Nested_ClosesInnerBeforeOuter() {
        var b = NewBuilder();
        b.Write("namespace Foo ");
        using (b.Block()) {
            b.Write("class Bar ");
            using (b.Block()) {
                b.WriteLine("int x;");
            }
        }

        AssertCode(b,
                   """
                   namespace Foo {
                       class Bar {
                           int x;
                       }
                   }
                   """);
    }

    [Test]
    public void Write_MultiLineText_ReindentsEachLine() {
        var b = NewBuilder();
        using (b.Indent()) {
            b.WriteLine("line1\r\nline2\r\nline3");
        }

        AssertCode(b,
                   """
                       line1
                       line2
                       line3

                   """);
    }

    [Test]
    public void WriteLine_MultiLineRawString_AtDepth_IndentsEachLine() {
        // Der Emitter-Fall: ein dedenteter Raw-String-Block wird innerhalb eines Indent-Scopes
        // geschrieben. Jede Zeile bekommt die aktuelle Einrückung; die Schlusszeile wird terminiert.
        var b = NewBuilder();
        using (b.Indent()) {
            b.WriteLine("""
                        #region Foo
                        /// <bar>value</bar>
                        #endregion
                        """);
        }

        AssertCode(b,
                   """
                       #region Foo
                       /// <bar>value</bar>
                       #endregion

                   """);
    }

    [Test]
    public void WriteLine_MultiLineRawString_PreservesRelativeIndent_OnTopOfCurrentIndent() {
        // Ein Block mit relativer Innen-Einrückung: die aktuelle Einrückung wird als Basis
        // vorangestellt, die relative Einrückung im Text bleibt additiv erhalten.
        var b = NewBuilder();
        using (b.Indent()) {
            b.WriteLine("""
                        public interface IFoo {
                            void Bar();
                        }
                        """);
        }

        AssertCode(b,
                   """
                       public interface IFoo {
                           void Bar();
                       }

                   """);
    }

    // -- Spaltenausrichtung (Align / der ST-anchor-Fall) ---------------------------------------------

    [Test]
    public void Column_TracksCursorPosition() {
        var b = NewBuilder();
        Assert.That(b.Column, Is.EqualTo(0));
        b.Write("abc");
        Assert.That(b.Column, Is.EqualTo(3));
        b.WriteLine();
        Assert.That(b.Column, Is.EqualTo(0));
    }

    [Test]
    public void Column_AtLineStart_IncludesPendingIndent() {
        var b = NewBuilder();
        using (b.Indent()) {
            // Noch nichts geschrieben: die 4 Spaces Einzug stehen noch aus, zählen aber zur Spalte.
            Assert.That(b.Column, Is.EqualTo(4));
        }
    }

    [Test]
    public void Align_AlignsWrappedList_ToFirstItemColumn() {
        // Reproduziert den ST-anchor-Fall aus WFSBase: die zweite Parameterzeile richtet sich an der
        // Spalte des ersten Parameters aus (nicht an der Einrück-Stufe). Die erwartete
        // Ausrichtungsspalte wird berechnet — das dokumentiert, warum der Einzug so tief ist — und
        // der Vergleich läuft exakt (inkl. CRLF).
        var b = NewBuilder();
        using (b.Indent())   // Stufe 1 → 4 Spaces … der Test bleibt handhabbar, Muster identisch zu 8
        using (b.Indent()) { // Stufe 2 → 8 Spaces (wie ein Klassen-Body)
            b.Write("public virtual IINIT_TASK Begin(");
            using (b.Align()) {
                b.WriteJoin(
                    ["TestInitParams p1", "int? nullableParam"],
                    p => b.Write(p),
                    separator: $",{b.NewLine}");
            }

            b.WriteLine(") {");
        }

        var padding = new string(' ', 8 + "public virtual IINIT_TASK Begin(".Length);
        Assert.That(b.ToString(), Is.EqualTo(
                        "        public virtual IINIT_TASK Begin(TestInitParams p1,\r\n" +
                        padding + "int? nullableParam) {\r\n"));
    }

    [Test]
    public void Align_Nested_UsesInnermostAnchor() {
        var b = NewBuilder();
        b.Write("aa");
        using (b.Align()) {        // Anker Spalte 2
            b.Write("bb");
            using (b.Align()) {    // Anker Spalte 4
                b.Write("cc\r\nX");
            }

            b.Write("\r\nY");      // wieder Anker Spalte 2
        }

        AssertCode(b,
                   """
                   aabbcc
                       X
                     Y
                   """);
    }

    // -- Trennlisten ---------------------------------------------------------------------------------

    [Test]
    public void WriteJoin_SingleItem_HasNoSeparator() {
        var b = NewBuilder();
        b.WriteJoin(["only"], s => b.Write(s), ", ");
        Assert.That(b.ToString(), Is.EqualTo("only"));
    }

    [Test]
    public void WriteJoin_InlineSeparator() {
        var b = NewBuilder();
        b.WriteJoin(["a", "b", "c"], s => b.Write(s), ", ");
        Assert.That(b.ToString(), Is.EqualTo("a, b, c"));
    }

    [Test]
    public void WriteAlignedJoin_MatchesManualAlignScope() {
        // Die Kurzform muss byte-gleich zum manuellen using (Align()) { WriteJoin(…) } sein: sie öffnet
        // den Anker an der aktuellen Spalte und joint darin. Erwartung wie im Align-Test berechnet.
        var b = NewBuilder();
        using (b.Indent())
        using (b.Indent()) {
            b.Write("public virtual IINIT_TASK Begin(");
            b.WriteAlignedJoin(
                ["TestInitParams p1", "int? nullableParam"], p => b.Write(p),
                separator: $",{b.NewLine}");
            b.WriteLine(") {");
        }

        var padding = new string(' ', 8 + "public virtual IINIT_TASK Begin(".Length);
        Assert.That(b.ToString(), Is.EqualTo(
                        "        public virtual IINIT_TASK Begin(TestInitParams p1,\r\n" +
                        padding + "int? nullableParam) {\r\n"));
    }

    [Test]
    public void WriteAlignedJoin_ReturnsSameBuilder_AndRestoresAnchor() {
        var b = NewBuilder();
        b.Write("x(");
        var result = b.WriteAlignedJoin(["a", "b"], p => b.Write(p), separator: $",{b.NewLine}");
        Assert.That(result, Is.SameAs(b));

        // Nach dem Aufruf ist der Anker wieder zu: eine Folgezeile richtet sich an der Einrückung
        // (Stufe 0) aus, nicht mehr an der Spalte des ersten Elements.
        b.Write($"{b.NewLine}y");
        Assert.That(b.ToString(), Is.EqualTo("x(a,\r\n  b\r\ny"));
    }

    [Test]
    public void FluentApi_ReturnsSameBuilder() {
        var b = NewBuilder();
        Assert.That(b.Write("x"),     Is.SameAs(b));
        Assert.That(b.WriteLine("y"), Is.SameAs(b));
        Assert.That(b.PushIndent(),   Is.SameAs(b));
        Assert.That(b.PopIndent(),    Is.SameAs(b));
    }

    [Test]
    public void MiniGolden_MethodWithWrappedParameters() {
        // Kleines Golden-Fixture als Vorbote des späteren Korpus-Beweises: ein vollständiger
        // Methoden-Rumpf mit umbrochener, ausgerichteter Parameterliste.
        var b = NewBuilder();
        b.Write("public abstract partial class Demo ");
        using (b.Block()) {
            b.Write("public void Do(");
            using (b.Align()) {
                b.WriteJoin(
                    ["int first", "string second"],
                    p => b.Write(p),
                    separator: $",{b.NewLine}");
            }

            b.Write(") ");
            using (b.Block()) {
                b.WriteLine("throw new NotImplementedException();");
            }
        }

        AssertCode(b,
                   """
                   public abstract partial class Demo {
                       public void Do(int first,
                                      string second) {
                           throw new NotImplementedException();
                       }
                   }
                   """);
    }
}
