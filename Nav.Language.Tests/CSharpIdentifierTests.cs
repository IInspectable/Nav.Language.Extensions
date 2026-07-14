using NUnit.Framework;

using Pharmatechnik.Nav.Language.CodeGen;

namespace Nav.Language.Tests;

[TestFixture]
public class CSharpIdentifierTests {

    // Charakterisierungstest für CSharp.IsValidIdentifier. Die Erwartungswerte sind gegen den
    // aktuellen Sprachstand abgeglichen (Roslyn SyntaxFacts.IsValidIdentifier + GetKeywordKind):
    // ein lexikalisch gültiger Bezeichner, der zugleich kein RESERVIERTES Keyword ist. Kontextuelle
    // Keywords (var/partial/where/record/yield/...) sind zulässige Bezeichner und daher gültig.

    static readonly string[] ValidIdentifiers = [
        "Foo", "_foo", "foo1", "_", "__", "_1",
        // Kontextuelle Keywords — gültige Bezeichner (früher via CodeDom teils fälschlich verworfen).
        "var", "async", "await", "dynamic", "partial", "yield", "nameof",
        "record", "where", "value", "global", "get", "set", "from", "select",
        // Nav-typische Namen, die keine C#-Keywords sind.
        "task", "init", "exit", "end", "choice",
        // Umlaute und weitere Unicode-Buchstaben.
        "Grüße", "Ärger", "Übung", "ößä", "straße", "café", "naïve",
        "µ", // µ  Ll  (Micro Sign)
        "π", // π  Ll  (Greek Small Letter Pi)
        "Ⅻ", // Ⅻ  Nl  (Roman Numeral Twelve — Startzeichen erlaubt)
        "ﬀ"  // ﬀ  Ll  (Latin Small Ligature ff)
    ];

    static readonly string[] InvalidIdentifiers = [
        "", " ",
        // Führende Ziffer.
        "1T", "2T", "1", "123",
        // Reservierte C#-Keywords — dürfen kein Bezeichner sein.
        "class", "int", "namespace", "static", "void", "return", "true", "false",
        "__arglist", "__makeref", "__reftype", "__refvalue", "stackalloc",
        // Verbatim-Präfix kommt in Nav-Namen nicht vor und wird nicht gesondert behandelt.
        "@class", "@Foo", "@1",
        // Unzulässige Zeichen.
        "Foo.Bar", "Foo Bar", "Foo-Bar",
        "５",   // ５  Nd  (Fullwidth Five — Ziffer, nicht als Startzeichen)
        "́abc" // führende kombinierende Marke (Mn) — kein Startzeichen
    ];

    [Test]
    [TestCaseSource(nameof(ValidIdentifiers))]
    public void IsValidIdentifier_ReturnsTrue(string value) {
        Assert.That(CSharp.IsValidIdentifier(value), Is.True,
                    $"'{value}' sollte ein gültiger C#-Bezeichner sein");
    }

    [Test]
    [TestCaseSource(nameof(InvalidIdentifiers))]
    public void IsValidIdentifier_ReturnsFalse(string value) {
        Assert.That(CSharp.IsValidIdentifier(value), Is.False,
                    $"'{value}' sollte KEIN gültiger C#-Bezeichner sein");
    }

    [Test]
    public void IsValidIdentifier_Null_ReturnsFalse() {
        Assert.That(CSharp.IsValidIdentifier(null), Is.False);
    }

}
