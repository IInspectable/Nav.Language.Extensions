namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Ein versionsgebundenes Sprach- oder Codegen-Feature der Nav-Sprache. Jeder Wert ist über
/// <see cref="NavLanguageFeatures.RequiredVersion"/> genau einer Mindest-<see cref="NavLanguageVersion"/>
/// zugeordnet.
/// </summary>
public enum NavLanguageFeature {

    /// <summary>
    /// Continuation-Kanten <c>… o-^ Task</c> / <c>… --^ Task</c> — der an einen GUI-Knoten gehängte
    /// Fortsetzungs-Task. Ab <see cref="NavLanguageVersion.Version2"/>.
    /// </summary>
    Continuation,

    /// <summary>
    /// Parameter-Klausel an einer <c>choice</c>-Deklaration (<c>choice X [params …]</c>), analog zum
    /// <c>init</c>-Knoten. Ab <see cref="NavLanguageVersion.Version2"/>.
    /// </summary>
    ChoiceParameters

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
        return feature switch {
            NavLanguageFeature.Continuation    => NavLanguageVersion.Version2,
            NavLanguageFeature.ChoiceParameters => NavLanguageVersion.Version2,
            // Ein künftiges, versionsloses Feature (oder ein unbekannter Wert) gilt als seit jeher verfügbar.
            _ => NavLanguageVersion.Default
        };
    }

    /// <summary>
    /// Ob <paramref name="feature"/> unter <paramref name="languageVersion"/> verfügbar ist (die aktive
    /// Version also mindestens die geforderte Mindestversion erreicht).
    /// </summary>
    public static bool IsAvailable(NavLanguageFeature feature, NavLanguageVersion languageVersion) {
        return languageVersion >= RequiredVersion(feature);
    }

}