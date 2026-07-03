using System.Collections.Immutable;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Ein versionsgebundenes Sprach- oder Codegen-Feature der Nav-Sprache. Jeder Wert ist über
/// <see cref="NavLanguageFeatures.RequiredVersion"/> genau einer Mindest-<see cref="NavLanguageVersion"/>
/// zugeordnet. Noch <b>ohne</b> Mitglieder — der erste Eintrag entsteht mit dem ersten Feature, das eine
/// höhere <c>#version</c> voraussetzt; bis dahin steht nur die Gate-Mechanik
/// (<see cref="NavLanguageFeatures"/>) bereit.
/// </summary>
public enum NavLanguageFeature {

}

/// <summary>
/// Das Versions-Gate der Nav-Sprache: entscheidet, ob ein <see cref="NavLanguageFeature"/> unter der in
/// einer Datei aktiven <see cref="NavLanguageVersion"/> verfügbar ist, und meldet andernfalls eine
/// <c>Nav5000</c>-Diagnose. Der Parser bleibt bewusst permissiv (er kennt stets die volle Syntax); die
/// Versions-Abhängigkeit ist eine rein semantische Prüfung — so entsteht statt eines kryptischen
/// Parse-Fehlers eine treffende Meldung samt Handlungsanweisung (<c>#version …</c> ergänzen).
/// </summary>
public static class NavLanguageFeatures {

    /// <summary>
    /// Die Mindest-<see cref="NavLanguageVersion"/>, ab der <paramref name="feature"/> verfügbar ist.
    /// Solange kein Feature registriert ist, gilt <see cref="NavLanguageVersion.Default"/>.
    /// </summary>
    public static NavLanguageVersion RequiredVersion(NavLanguageFeature feature) {
        // Noch keine versionsgebundenen Features registriert — jeder künftige Eintrag kommt hierher.
        return NavLanguageVersion.Default;
    }

    /// <summary>
    /// Ob <paramref name="feature"/> unter <paramref name="languageVersion"/> verfügbar ist (die aktive
    /// Version also mindestens die geforderte Mindestversion erreicht).
    /// </summary>
    public static bool IsAvailable(NavLanguageFeature feature, NavLanguageVersion languageVersion) {
        return languageVersion >= RequiredVersion(feature);
    }

    /// <summary>
    /// Prüft die Verfügbarkeit von <paramref name="feature"/> unter <paramref name="languageVersion"/> und
    /// meldet — falls die Version zu niedrig ist — eine <c>Nav5000</c>-Diagnose an <paramref name="location"/>
    /// in <paramref name="diagnostics"/>. Liefert <c>true</c>, wenn das Feature verfügbar ist.
    /// </summary>
    public static bool ReportIfUnavailable(NavLanguageFeature feature,
                                           NavLanguageVersion languageVersion,
                                           Location location,
                                           ImmutableArray<Diagnostic>.Builder diagnostics) {

        var required = RequiredVersion(feature);
        if (languageVersion >= required) {
            return true;
        }

        diagnostics.Add(new Diagnostic(location,
                                       DiagnosticDescriptors.Semantic.Nav5000Feature0RequiresNavLanguageVersion1,
                                       feature, required));
        return false;
    }

}