#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;

/// <summary>
/// Annotation an einem generierten Choice-Forward (<c>next.{Choice}(…)</c>) — der Rückverweis aus dem
/// <c>&lt;NavChoiceCall&gt;</c>-Tag des aufgerufenen Forwards auf den Choice-Knoten der Nav-Task.
/// Aufrufseitiges Gegenstück zur <see cref="NavChoiceAnnotation"/> an der <c>{Choice}Logic</c>-Deklaration:
/// verortet die Aufrufstelle und trägt zusätzlich den C#→C#-Sprung zur zugehörigen Logic-Implementierung.
/// </summary>
public partial class NavChoiceCallAnnotation: NavInvocationAnnotation {

    /// <summary>
    /// Erzeugt die Choice-Forward-Annotation aus dem am aufgerufenen Forward gefundenen Tag.
    /// </summary>
    /// <param name="taskAnnotation">Die Task-Annotation, deren Herkunft übernommen wird.</param>
    /// <param name="identifier">Der Methoden-Bezeichner an der Aufrufstelle — der navigierbare Anker.</param>
    /// <param name="choiceName">Der Name des Choice-Knotens (aus dem <c>&lt;NavChoiceCall&gt;</c>-Tag).</param>
    /// <param name="wfsBaseFullyQualifiedName">Der voll qualifizierte Name der <c>{Task}WFSBase</c>, die den
    /// Forward und die abstrakte <c>{Choice}Logic</c> trägt (siehe <see cref="WfsBaseFullyQualifiedName"/>).</param>
    public NavChoiceCallAnnotation(NavTaskAnnotation taskAnnotation,
                                   IdentifierNameSyntax identifier,
                                   string choiceName,
                                   string wfsBaseFullyQualifiedName)
        : base(taskAnnotation, identifier) {

        ChoiceName               = choiceName ?? String.Empty;
        WfsBaseFullyQualifiedName = wfsBaseFullyQualifiedName ?? String.Empty;
    }

    /// <summary>
    /// Der Name des Choice-Knotens, auf den die Annotation zurückverweist.
    /// </summary>
    [NotNull]
    public string ChoiceName { get; }

    /// <summary>
    /// Der voll qualifizierte Name der <c>{Task}WFSBase</c>, die den aufgerufenen <c>{Choice}(…)</c>-Forward
    /// (und die abstrakte <c>{Choice}Logic</c>) trägt — am Leseort aus dem Forward-Symbol bestimmt. Trägt den
    /// C#→C#-Sprung von der Aufrufstelle zur <c>{Choice}Logic</c>-Implementierung (Abstieg auf die abgeleiteten
    /// Klassen, siehe <c>LocationFinder</c>).
    /// </summary>
    [NotNull]
    public string WfsBaseFullyQualifiedName { get; }
}
