#region Using Directives

using System;
using Microsoft.CodeAnalysis;

using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Notification; 

/// <summary>
/// Ereignisdaten für die Meldung, dass sich die Klassen-Annotation (die Nav↔C#-Verknüpfung) eines
/// generierten Dokuments geändert hat.
/// </summary>
public class ClassAnnotationChangedArgs : EventArgs {

    /// <summary>Das Roslyn-<see cref="DocumentId"/> des betroffenen generierten C#-Dokuments.</summary>
    public DocumentId        DocumentId     { get; set; }
    /// <summary>Die geänderte <see cref="NavTaskAnnotation"/>, die Nav-Task und generierte Klasse verknüpft.</summary>
    public NavTaskAnnotation TaskAnnotation { get; set; }
}

/// <summary>
/// Empfänger für Änderungen an Klassen-Annotationen; über <see cref="NotificationService"/> registriert.
/// </summary>
interface IClassAnnotationChangeListener {
    /// <summary>Wird aufgerufen, wenn sich eine Klassen-Annotation geändert hat.</summary>
    void OnClassAnnotationsChanged(object sender, ClassAnnotationChangedArgs e);
}

/// <summary>
/// Prozessweite Vermittlung von Klassen-Annotations-Änderungen: Melder rufen
/// <see cref="RaiseClassAnnotationChanged"/>, Empfänger registrieren sich über
/// <see cref="AddClassAnnotationChangeListener"/>. Die Empfänger werden nur schwach gehalten (siehe
/// <see cref="WeakListenerManager{T}"/>), sodass die Registrierung sie nicht am Leben hält.
/// </summary>
static class NotificationService {

    static readonly WeakListenerManager<IClassAnnotationChangeListener> ClassAnnotationChangeListener;

    static NotificationService() {
        ClassAnnotationChangeListener = new WeakListenerManager<IClassAnnotationChangeListener>();
    }

    #region ClassAnnotationChanged

    /// <summary>Benachrichtigt alle registrierten Empfänger über eine geänderte Klassen-Annotation.</summary>
    public static void RaiseClassAnnotationChanged(object sender, ClassAnnotationChangedArgs e) {
        ClassAnnotationChangeListener.InvokeListener(listener => listener.OnClassAnnotationsChanged(sender, e));
    }

    /// <summary>Registriert <paramref name="listener"/> für künftige Änderungsmeldungen (schwach gehalten).</summary>
    public static void AddClassAnnotationChangeListener(IClassAnnotationChangeListener listener) {
        ClassAnnotationChangeListener.AddListener(listener);
    }

    /// <summary>Hebt die Registrierung von <paramref name="listener"/> wieder auf.</summary>
    public static void RemoveClassAnnotationChangeListener(IClassAnnotationChangeListener listener) {
        ClassAnnotationChangeListener.RemoveListener(listener);
    }

    #endregion
}