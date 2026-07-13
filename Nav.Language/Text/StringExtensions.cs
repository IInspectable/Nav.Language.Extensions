#region Using Directives

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

#endregion

// ReSharper disable ForCanBeConvertedToForeach

namespace Pharmatechnik.Nav.Language.Text;

public static class StringExtensions {

    public static bool IsEmpty(this string value) {
        return value.Length == 0;
    }

    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value) {
        return String.IsNullOrEmpty(value);
    }

    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? value) {
        return String.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Liefert null, wenn die angegebene Zeichenfolge null oder String.Empty ist;
    /// andernfalls wird die angegebene Zeichenfolge zurückgegeben.
    /// </summary>
    public static string? NullIfEmpty(this string? value) {
        return value.IsNullOrEmpty() ? null : value;
    }

    /// <summary>
    /// Liefert null, wenn die angegebene Zeichenfolge null oder String.Empty ist, oder nur aus Whitespaces besteht;
    /// andernfalls wird die angegebene Zeichenfolge zurückgegeben.
    /// </summary>
    public static string? NullIfWhiteSpace(this string? value) {
        return value.IsNullOrWhiteSpace() ? null : value;
    }

    public static string Substring(this string text, TextExtent extent) {
        if (extent.IsMissing) {
            return String.Empty;
        }

        return text.Substring(startIndex: extent.Start, length: extent.Length);
    }

    public static ReadOnlySpan<char> Slice(this ReadOnlySpan<char> span, TextExtent extent) {
        if (extent.IsMissing) {
            return ReadOnlySpan<char>.Empty;
        }

        return span.Slice(start: extent.Start, length: extent.Length);
    }

    /// <summary>
    /// Liefert den vorigen Identifier, oder String.Empty, falls es keinen vorigen Identifier gibt.
    /// </summary>
    public static string GetPreviousIdentifier(this string text, int position) {
        return text.Substring(GetExtentOfPreviousIdentifier(text.AsSpan(), position));
    }

    /// <summary>
    /// Liefert den vorigen Identifier, oder Empty, falls es keinen vorigen Identifier gibt.
    /// </summary>
    public static ReadOnlySpan<char> GetPreviousIdentifier(this ReadOnlySpan<char> span, int position) {
        return span.Slice(GetExtentOfPreviousIdentifier(span, position));
    }

    /// <summary>
    /// Liefer den Bereich des Identifiers, der vor position ist.
    /// Identifier Foo
    /// ------------^
    /// Liefert den Bereich von Identifier, also [0,10( 
    /// </summary>
    public static TextExtent GetExtentOfPreviousIdentifier(this string text, int position) {
        return GetExtentOfPreviousIdentifier(text.AsSpan(), position);
    }

    /// <summary>
    /// Liefer den Bereich des Identifiers, der vor position ist.
    /// Identifier Foo
    /// -----------^
    /// Liefert den Bereich von Identifier, also [0,10( 
    /// </summary>
    public static TextExtent GetExtentOfPreviousIdentifier(this ReadOnlySpan<char> span, int position) {

        var identifierStart = GetStartOfIdentifier(span, position);
        if (identifierStart != -1) {
            position = identifierStart;
        }

        var previousIdentiferEnd = span.IndexOfPreviousNonWhitespace(position);
        if (previousIdentiferEnd == -1) {
            return TextExtent.Missing;
        }

        var previousIdentiferStart = span.GetStartOfIdentifier(previousIdentiferEnd);
        if (previousIdentiferStart == -1) {
            return TextExtent.Missing;
        }

        return TextExtent.FromBounds(start: previousIdentiferStart, end: previousIdentiferEnd + 1);
    }

    public static int IndexOfPreviousNonWhitespace(this string text, int position) {
        return IndexOfPreviousNonWhitespace(text.AsSpan(), position);
    }

    public static int IndexOfPreviousNonWhitespace(this ReadOnlySpan<char> span, int position) {

        if (position == 0) {
            return -1;
        }

        do {
            position -= 1;
        } while (position > 0 && char.IsWhiteSpace(span[position]));

        return position;
    }

    public static int GetStartOfIdentifier(this string text, int position) {
        return GetStartOfIdentifier(text.AsSpan(), position);
    }

    public static int GetStartOfIdentifier(this ReadOnlySpan<char> span, int position) {

        if (position < 0 || position >= span.Length) {
            throw new IndexOutOfRangeException();
        }

        if (!SyntaxFacts.IsIdentifierCharacter(span[(position)])) {
            return -1;
        }

        int start = position;
        while (start > 0 && SyntaxFacts.IsIdentifierCharacter(span[(start - 1)])) {
            start -= 1;
        }

        return start;
    }

    public static bool IsInTextBlock(this string text, int position, char blockStartChar, char blockEndChar) {
        return text.AsSpan().IsInTextBlock(position, blockStartChar, blockEndChar);
    }

    public static bool IsInTextBlock(this ReadOnlySpan<char> text, int position, char blockStartChar, char blockEndChar) {
        return IsInTextBlockImpl(text, position, blockStartChar, blockEndChar, out _);
    }

    public static bool IsInQuotation(this string text, int position, char quotationChar = '"') {
        return text.AsSpan().IsInQuotation(position, quotationChar);
    }

    public static bool IsInQuotation(this ReadOnlySpan<char> text, int position, char quotationChar = '"') {
        return IsInTextBlockImpl(text, position, quotationChar, out _);
    }

    /// <summary>
    /// Liefert den gequoteten Bereich um position. Wenn der Bereich nach hinten offen ist, d.h. nicht
    /// explizit mit dem angegebenen quotationChar abschließt, hört der Bereich mit dem ersten
    /// Whitespace nach position auf.
    /// Gibt es weder ein abschließendes quotationChar noch ein terminierendes Whitespace,
    /// wird der Bereich vom Anfang der quitierung bis zum Ende der angegebenen Zeichenfolge
    /// zurückgeliefert
    /// </summary>
    public static TextExtent QuotedExtent(this string text, int position, char quotationChar = '"', bool includequotationCharInExtent = false) {
        return text.AsSpan().QuotedExtent(position, quotationChar, includequotationCharInExtent);
    }

    public static TextExtent QuotedExtent(this ReadOnlySpan<char> text, int position, char quotationChar = '"', bool includequotationCharInExtent = false) {

        if (!IsInTextBlockImpl(text, position, quotationChar, out var start)) {
            return TextExtent.Missing;
        }

        int offset = 0;
        if (includequotationCharInExtent) {
            offset = 1;
        }

        start++;
        int firstWhiteSpace = -1;
        for (int index = start; index < text.Length; index++) {
            if (text[index] == quotationChar) {

                return TextExtent.FromBounds(start: start - offset, end: index + offset);
            }

            if (firstWhiteSpace == -1 && Char.IsWhiteSpace(text[index])) {
                firstWhiteSpace = index;
            }
        }

        if (firstWhiteSpace != -1) {
            return TextExtent.FromBounds(start: start - offset, end: firstWhiteSpace);
        }

        return TextExtent.FromBounds(start: start - offset, end: text.Length);
    }

    static bool IsInTextBlockImpl(this ReadOnlySpan<char> text, int position, char quotationChar, out int quotationStart) {

        quotationStart = -1;

        if (position < 0 || position > text.Length) {
            return false;
        }

        bool inQuotation = false;
        for (int index = 0; index < position; index++) {
            if (text[index] == quotationChar) {

                inQuotation ^= true;
                if (inQuotation) {
                    quotationStart = index;
                }
            }

        }

        return inQuotation;
    }

    // ReSharper disable once OutParameterValueIsAlwaysDiscarded.Local
    static bool IsInTextBlockImpl(this ReadOnlySpan<char> text, int position, char blockStartChar, char blockEndChar, out int quotationStart) {

        if (blockStartChar == blockEndChar) {
            return IsInTextBlockImpl(text, position, blockStartChar, out quotationStart);
        }

        quotationStart = -1;

        if (position < 0 || position > text.Length) {
            return false;
        }

        // TODO Was ist mit nested Blocks?

        int blockEntered = 0;
        for (int index = 0; index < position; index++) {

            if (text[index] == blockStartChar) {
                blockEntered++;
                quotationStart = index;
            } else if (text[index] == blockEndChar && blockEntered > 0) {
                blockEntered--;
            }

        }

        return blockEntered > 0;
    }

    public static string ToCamelcase(this string s) {

        if (String.IsNullOrEmpty(s)) {
            return String.Empty;
        }

        return s.Substring(0, 1).ToLowerInvariant() + s.Substring(1);
    }

    public static string ToPascalcase(this string s) {

        if (String.IsNullOrEmpty(s)) {
            return String.Empty;
        }

        return s.Substring(0, 1).ToUpperInvariant() + s.Substring(1);
    }

    /// <summary>
    /// Liefert den Spaltenindex (beginnend bei 0) für den angegebenen Offset vom Start der Zeile. 
    /// Es werden Tabulatoren entsprechend eingerechnet.
    /// </summary>
    /// <example>
    /// Gegeben sei folgende Zeile mit gemischten Leerzeichen (o) und Tabulatoren (->) mit einer Tabulatorweite 
    /// von 4 und anschließendem Text (T). Der angeforderte Offset ist 4:
    /// TT->--->TTTTTT
    /// ^^-^---^
    /// Der Spaltenindex für den Zeichenindex 4 ist 8 (man beachte die 2 Tabulatoren!).
    /// </example>
    public static int GetColumnForOffset(this ReadOnlySpan<char> text, int tabSize, int offset) {
        var column = 0;
        for (int index = 0; index < offset; index++) {
            var c = text[index];
            if (c == '\t') {
                column += tabSize - column % tabSize;
            } else {
                column++;
            }
        }

        return column;
    }

    /// <summary>
    /// Liefert den Spaltenindex (beginnend bei 0) für das erste Signifikante Zeichen in der angegebenen Zeile.
    /// Als nicht signifikant gelten alle Arten von Leerzeichen. Dabei werden Tabulatoren entsprechend umgerechnet.
    /// </summary>
    /// <example>
    /// Gegeben sei folgende Zeile mit gemischten Leerzeichen (o) und Tabulatoren (->) mit einer Tabulatorweite 
    /// von 4 und anschließendem Text (T):
    /// --->oo->TTTTTT
    /// --------^ 
    /// Der Signifikante Spaltenindex für diese Zeile ist 8.
    /// </example>
    public static int GetSignificantColumn(this ReadOnlySpan<char> text, int tabSize) {
        bool hasSignificantContent = false;
        int  column                = 0;
        for (int index = 0; index < text.Length; index++) {
            var c = text[index];

            if (c == '\t') {
                column += tabSize - column % tabSize;
            } else if (Char.IsWhiteSpace(c)) {
                column++;
            } else {
                hasSignificantContent = true;
                break;
            }
        }

        return hasSignificantContent ? column : Int32.MaxValue;
    }

    public static ImmutableArray<int> ParseLineStarts(this ReadOnlySpan<char> text) {

        if (text.Length == 0) {
            return ImmutableArray.Create(0);
        }

        // Kapazität grob vorbelegen (~32 Zeichen/Zeile), damit der Builder nicht mehrfach wachsen
        // muss; DrainToImmutable übergibt das Array am Ende ohne die ToImmutable-Abschluss-Kopie.
        var lineStarts = ImmutableArray.CreateBuilder<int>(initialCapacity: text.Length / 32 + 1);

        int index;
        int lineStart = 0;
        for (index = 0; index < text.Length; index++) {

            char c = text[index];

            bool isNewLine = false;

            if (c == '\n') {
                isNewLine = true;
            } else if (c == '\r') {
                isNewLine = true;
                // => \r\n
                if (index + 1 < text.Length && text[index + 1] == '\n') {
                    index++;
                }
            }

            if (isNewLine) {
                // Achtung: Extent End zeigt immer _hinter_ das letzte Zeichen!
                var lineEnd = index + 1;
                lineStarts.Add(lineStart);
                lineStart = lineEnd;
            }
        }

        // Einzige/letzte Zeile nicht vergessen.
        if (index >= lineStart) {
            lineStarts.Add(lineStart);
        }

        return lineStarts.DrainToImmutable();
    }

    /// <summary>
    /// Liefert die Anzahl an Zeichen des Zeilenvorschubs, oder 0, falls der Text nicht mit einem Zeilenvorschub endet.
    /// </summary>
    public static int GetNewLineCharCount(this string text) {
        return GetNewLineCharCount(text.AsSpan());
    }

    /// <summary>
    /// Liefert die Anzahl an Zeichen des Zeilenvorschubs, oder 0, falls der Text nicht mit einem Zeilenvorschub endet.
    /// </summary>
    public static int GetNewLineCharCount(this ReadOnlySpan<char> text) {

        if (text.Length >= 1) {
            if (text[text.Length - 1] == '\n') {

                if (text.Length >= 2 && text[text.Length - 2] == '\r') {
                    // \r\n
                    return 2;
                }

                // \n
                return 1;
            }

        }

        return 0;
    }

}