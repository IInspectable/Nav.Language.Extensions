#region Using Directives

using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Eine einzelne, zu einem Regulären Ausdruck kompilierte Zeile einer <c>.navignore</c>-Datei (gitignore-Syntax).
/// Gematcht wird gegen einen <b>relativen</b>, mit <c>/</c> getrennten und kleingeschriebenen Pfad
/// (relativ zum Verzeichnis der <c>.navignore</c>-Datei).
/// </summary>
/// <remarks>
/// Unterstützte gitignore-Teilmenge: Kommentare (<c>#</c>), Leerzeilen, Negation (<c>!</c>), Verankerung über
/// <c>/</c> (am Anfang oder in der Mitte), Verzeichnis-Muster (abschließendes <c>/</c>), die Platzhalter
/// <c>*</c> (kein <c>/</c>), <c>?</c> (kein <c>/</c>) und <c>**</c> (über Verzeichnisgrenzen). Bewusst NICHT
/// unterstützt (kommen in <c>.nav</c>-Pfaden praktisch nicht vor): Zeichenklassen <c>[a-z]</c> (<c>[</c>/<c>]</c>
/// werden literal behandelt) und das Escapen abschließender Leerzeichen.
/// </remarks>
sealed class NavIgnorePattern {

    NavIgnorePattern(bool isNegated, bool dirOnly, Regex regex, string source) {
        IsNegated = isNegated;
        DirOnly   = dirOnly;
        Regex     = regex;
        Source    = source;
    }

    /// <summary>Führendes <c>!</c> — hebt einen vorherigen Treffer wieder auf.</summary>
    public bool IsNegated { get; }

    /// <summary>Abschließendes <c>/</c> — greift nur auf Verzeichnisse (also Dateien unterhalb).</summary>
    public bool DirOnly { get; }

    public Regex  Regex  { get; }
    public string Source { get; }

    public bool IsMatch(string relativePathPosix) => Regex.IsMatch(relativePathPosix);

    /// <summary>
    /// Parst eine Zeile. Liefert <c>null</c> für Kommentare, Leerzeilen und Zeilen ohne verwertbares Muster.
    /// </summary>
    public static NavIgnorePattern? TryParse(string rawLine) {

        if (rawLine == null) {
            return null;
        }

        // Abschließende Whitespaces verwerfen (gitignore: nicht-escapte Trailing-Spaces sind unbedeutend;
        // das Escapen via "\ " unterstützen wir bewusst nicht) sowie ein evtl. anhängendes CR bei \n-Split.
        var line = rawLine.TrimEnd('\r').TrimEnd(' ', '\t');

        if (line.Length == 0 || line[0] == '#') {
            return null; // Leerzeile oder Kommentar
        }

        var negated = false;

        if (line[0] == '\\' && line.Length > 1 && (line[1] == '#' || line[1] == '!')) {
            // Escaptes erstes Sonderzeichen: '#'/'!' literal behandeln.
            line = line.Substring(1);
        } else if (line[0] == '!') {
            negated = true;
            line    = line.Substring(1);
            if (line.Length == 0) {
                return null;
            }
        }

        var dirOnly = false;
        if (line.EndsWith("/")) {
            dirOnly = true;
            line    = line.Substring(0, line.Length - 1);
            if (line.Length == 0) {
                return null;
            }
        }

        var anchored = false;
        if (line.StartsWith("/")) {
            anchored = true;
            line     = line.Substring(1);
            if (line.Length == 0) {
                return null;
            }
        }

        // Ein Separator irgendwo (außer dem bereits entfernten abschließenden) verankert das Muster relativ
        // zum .navignore-Verzeichnis; ohne Separator greift es auf jeder Tiefe (gitignore-Regel).
        if (line.Contains("/")) {
            anchored = true;
        }

        var body   = TranslateToRegexBody(line.ToLowerInvariant());
        var prefix = anchored ? "^" : "^(?:.*/)?";
        // Nicht-Verzeichnis-Muster matchen die Datei selbst UND (falls Verzeichnis) deren Unterbaum;
        // Verzeichnis-Muster verlangen mindestens ein weiteres Segment (eine Datei darunter).
        var suffix = dirOnly ? "/.*$" : "(?:/.*)?$";

        var regex = new Regex(prefix + body + suffix, RegexOptions.Compiled | RegexOptions.CultureInvariant);

        return new NavIgnorePattern(negated, dirOnly, regex, rawLine);
    }

    /// <summary>
    /// Übersetzt den Musterrumpf (bereits kleingeschrieben, ohne führenden/abschließenden Separator-Sonderfall)
    /// in einen Regex-Teilausdruck, der gegen einen <c>/</c>-getrennten relativen Pfad matcht.
    /// </summary>
    static string TranslateToRegexBody(string pattern) {

        var sb = new StringBuilder();
        var i  = 0;

        while (i < pattern.Length) {

            var c = pattern[i];

            if (c == '*') {

                var isDouble = i + 1 < pattern.Length && pattern[i + 1] == '*';

                if (isDouble) {

                    var slashBefore = i == 0 || pattern[i - 1] == '/';

                    // alle aufeinanderfolgenden '*' zusammenfassen
                    var j = i;
                    while (j < pattern.Length && pattern[j] == '*') {
                        j++;
                    }

                    var slashAfter = j < pattern.Length && pattern[j] == '/';

                    if (slashBefore && slashAfter) {
                        // "**/" — null oder mehr Verzeichnisebenen
                        sb.Append("(?:.*/)?");
                        i = j + 1; // den folgenden '/' mitverbrauchen
                    } else if (slashBefore && j == pattern.Length) {
                        // abschließendes "/**" bzw. reines "**" — alles darunter
                        sb.Append(".*");
                        i = j;
                    } else {
                        // nicht von Separatoren umschlossen → wie ein einfaches '*'
                        sb.Append("[^/]*");
                        i = j;
                    }

                } else {
                    sb.Append("[^/]*");
                    i++;
                }

            } else if (c == '?') {
                sb.Append("[^/]");
                i++;
            } else if (c == '/') {
                sb.Append('/');
                i++;
            } else {
                sb.Append(Regex.Escape(c.ToString()));
                i++;
            }
        }

        return sb.ToString();
    }

}