#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.FindReferences;

using Location = Pharmatechnik.Nav.Language.Location;

#endregion

namespace Nav.Language.CodeAnalysis.Tests;

/// <summary>
/// Serialisiert ein Navigationsergebnis (eine <see cref="Location"/> bzw. eine Menge von
/// <see cref="Location"/>/<see cref="ReferenceItem"/>) zu deterministischem, <b>maschinenunabhängigem</b>
/// Text für die Golden-Snapshots. Je Ziel eine Zeile
/// <c>&lt;datei-tag&gt; (startZeile,startSpalte)-(endZeile,endSpalte)  "zieltext"</c> (1-basiert, Format
/// wie beim <c>UnitTestDiagnosticFormatter</c>) plus eine eingerückte Kontextzeile mit dem Quelltext.
/// Der exakte Span pinnt <i>welches</i> Vorkommen getroffen wird — ein Sprung auf eine andere Fundstelle
/// desselben Namens ändert den Span und schlägt im Golden-Diff an.
/// </summary>
static class NavigationSnapshot {

    public static string Serialize(Location location, CodeAnalysisTestContext context) {

        if (location == null) {
            throw new ArgumentNullException(nameof(location));
        }

        var sb = new StringBuilder();
        AppendEntry(sb, location, context);
        return sb.ToString();
    }

    public static string Serialize(IEnumerable<Location> locations, CodeAnalysisTestContext context) {

        // Stabile Reihenfolge: erst nach Datei-Tag, dann nach Startposition — unabhängig von der
        // Fund-Reihenfolge des Finders.
        var ordered = locations.OrderBy(location => context.FileTag(location), StringComparer.Ordinal)
                               .ThenBy(location => location.Start);

        var sb = new StringBuilder();
        foreach (var location in ordered) {
            AppendEntry(sb, location, context);
        }

        return sb.ToString();
    }

    public static string Serialize(IEnumerable<ReferenceItem> references, CodeAnalysisTestContext context) {
        return Serialize(references.Select(reference => reference.Location), context);
    }

    static void AppendEntry(StringBuilder sb, Location location, CodeAnalysisTestContext context) {

        sb.Append(context.FileTag(location));
        sb.Append(' ');
        sb.Append($"({location.StartLine + 1},{location.StartCharacter + 1})-({location.EndLine + 1},{location.EndCharacter + 1})");
        sb.Append("  \"");
        sb.Append(context.TextAt(location));
        sb.Append("\"\n");

        sb.Append("    ");
        sb.Append(context.SourceLineAt(location));
        sb.Append('\n');
    }

}
