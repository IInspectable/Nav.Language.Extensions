#region Using Directives

using System;
using System.IO;

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Utilities; 

/// <summary>
/// Hilfsfunktionen, die aus einem Pfad einen Verzeichnis-<see cref="Uri"/> bilden. Kernpunkt ist, dass
/// der Pfad zuverlässig als Verzeichnis (nicht als Datei) erkannt wird — dafür wird ein abschließender
/// Verzeichnistrenner sichergestellt. Die so gebildeten Verzeichnis-Uris nutzt der <see cref="ProjectMapper"/>
/// über <see cref="Uri.IsBaseOf"/>, um eine Datei ihrem Projektverzeichnis zuzuordnen.
/// </summary>
static class UriBuilder {

    /// <summary>
    /// Bildet aus einem Verzeichnispfad einen Verzeichnis-<see cref="Uri"/> und ergänzt bei Bedarf den
    /// abschließenden Trenner. Liefert <c>null</c> bei leerer Eingabe.
    /// </summary>
    /// <param name="directory">Der Verzeichnispfad.</param>
    [CanBeNull]
    public static Uri BuildDirectoryUriFromDirectory(string directory) {

        if (String.IsNullOrEmpty(directory)) {
            return null;
        }

        // Nur wenn der Pfad mit einem \ oder / endet, ist sichergestellt, dass der Uri als Verzeichnis und nicht
        // als Datei erkannt wird. Sobald nämlich der Pfad einen Punkt im letzten Verzeichnis enthält, wird dieser als Datei gesehen
        // Beispiel: "c:\ws\Nav.Project" wird als Datei gesehen, "c:\ws\Nav.Project\" dagegen als Verzeichnis
        if (!directory.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
            !directory.EndsWith(Path.AltDirectorySeparatorChar.ToString())) {

            directory += Path.DirectorySeparatorChar;
        }

        return new Uri(directory);

    }

    /// <summary>
    /// Bildet den Verzeichnis-<see cref="Uri"/> des Ordners, der die Datei <paramref name="fileName"/>
    /// enthält. Liefert <c>null</c> bei leerer Eingabe.
    /// </summary>
    /// <param name="fileName">Der Dateipfad, dessen Verzeichnis abgeleitet wird.</param>
    [CanBeNull]
    public static Uri BuildDirectoryUriFromFile(string fileName) {
        if (String.IsNullOrEmpty(fileName)) {
            return null;
        }

        return BuildDirectoryUriFromDirectory(Path.GetDirectoryName(fileName));
    }

}