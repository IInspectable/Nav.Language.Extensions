using System;
using System.Collections.Immutable;
using System.Globalization;

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// C#-spezifische Hilfen für die Codegenerierung.
/// </summary>
static class CSharp {

    /// <summary>
    /// Ob <paramref name="value"/> ein gültiger C#-Bezeichner ist — also unverändert als
    /// generierter Klassen-/Member-Name taugt. Ein Nav-Name, der diese Prüfung nicht besteht,
    /// würde beim Übersetzen des erzeugten C#-Codes brechen.
    /// </summary>
    /// <remarks>
    /// Die Regel ist der aktuelle Sprachstand (spiegelt Roslyns
    /// <c>SyntaxFacts.IsValidIdentifier</c> + <c>GetKeywordKind</c>): ein lexikalisch gültiger
    /// Bezeichner (Startzeichen + Folgezeichen nach den Unicode-Kategorien der C#-Spezifikation),
    /// der zugleich kein <b>reserviertes</b> Schlüsselwort ist. Kontextuelle Schlüsselwörter
    /// (<c>var</c>, <c>partial</c>, <c>where</c>, <c>record</c>, <c>yield</c>, …) sind zulässige
    /// Bezeichner und werden daher akzeptiert. Ein Verbatim-Präfix (<c>@name</c>) kommt in
    /// Nav-Namen nicht vor und wird nicht gesondert behandelt.
    /// </remarks>
    public static bool IsValidIdentifier(string? value) {

        // Bewusst kein string.IsNullOrEmpty: die netstandard2.0-BCL trägt keine Nullable-
        // Annotationen, erst der explizite null-Vergleich verengt den Wert für die Flussanalyse.
        if (value is null || value.Length == 0) {
            return false;
        }

        if (!IsIdentifierStartCharacter(value[0])) {
            return false;
        }

        for (var i = 1; i < value.Length; i++) {
            if (!IsIdentifierPartCharacter(value[i])) {
                return false;
            }
        }

        return !ReservedKeywords.Contains(value);
    }

    // Reservierte C#-Schlüsselwörter — die einzigen Wörter, die (unescaped) kein Bezeichner sein
    // dürfen. Kontextuelle Schlüsselwörter stehen bewusst NICHT drin, weil sie als Bezeichner
    // zulässig sind. Menge und Schreibweise entsprechen Roslyns GetKeywordKind (case-sensitiv).
    static readonly ImmutableHashSet<string> ReservedKeywords = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "__arglist", "__makeref", "__reftype", "__refvalue",
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while");

    // Zulässiges erstes Zeichen eines Bezeichners: '_' oder ein Buchstabe (Unicode Lu/Ll/Lt/Lm/Lo/Nl).
    static bool IsIdentifierStartCharacter(char c) {
        return c == '_' || IsLetterCategory(CharUnicodeInfo.GetUnicodeCategory(c));
    }

    // Zulässiges Folgezeichen: Startzeichen-Kategorien plus Ziffern (Nd), Verbindungszeichen (Pc,
    // enthält '_'), kombinierende Marken (Mn/Mc) und Formatzeichen (Cf) — gemäß C#-Spezifikation.
    static bool IsIdentifierPartCharacter(char c) {
        var category = CharUnicodeInfo.GetUnicodeCategory(c);
        return IsLetterCategory(category)                        ||
               category == UnicodeCategory.DecimalDigitNumber   ||
               category == UnicodeCategory.ConnectorPunctuation ||
               category == UnicodeCategory.NonSpacingMark       ||
               category == UnicodeCategory.SpacingCombiningMark ||
               category == UnicodeCategory.Format;
    }

    static bool IsLetterCategory(UnicodeCategory category) {
        return category is UnicodeCategory.UppercaseLetter
                        or UnicodeCategory.LowercaseLetter
                        or UnicodeCategory.TitlecaseLetter
                        or UnicodeCategory.ModifierLetter
                        or UnicodeCategory.OtherLetter
                        or UnicodeCategory.LetterNumber;
    }
}
