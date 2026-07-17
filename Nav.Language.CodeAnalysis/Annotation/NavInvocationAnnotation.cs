#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation; 

/// <summary>
/// Basisklasse aller Annotationen, die an einer <em>Aufrufstelle</em> hängen — dem Init-Aufruf
/// (<see cref="NavInitCallAnnotation"/>) bzw. dem Choice-Forward (<see cref="NavChoiceCallAnnotation"/>).
/// Ergänzt die von <see cref="NavTaskAnnotation"/> geerbte Task-Herkunft um den
/// <see cref="Identifier"/> der aufgerufenen Methode — den navigierbaren Anker der Aufrufstelle.
/// Im Gegensatz zu <see cref="NavMethodAnnotation"/> zeigt sie nicht auf die Deklaration eines Members,
/// sondern auf dessen Verwendung.
/// </summary>
public abstract class NavInvocationAnnotation: NavTaskAnnotation {

    /// <summary>
    /// Übernimmt die Task-Herkunft aus <paramref name="taskAnnotation"/> und bindet die Annotation an den
    /// Bezeichner der Aufrufstelle.
    /// </summary>
    /// <param name="taskAnnotation">Die Task-Annotation, deren Herkunft übernommen wird.</param>
    /// <param name="identifier">Der Methoden-Bezeichner an der Aufrufstelle — der navigierbare Anker.</param>
    /// <exception cref="ArgumentNullException"><paramref name="identifier"/> ist <see langword="null"/>.</exception>
    protected NavInvocationAnnotation(NavTaskAnnotation taskAnnotation, 
                                      IdentifierNameSyntax identifier): base(taskAnnotation) {

        Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
    }

    /// <summary>
    /// Der Bezeichner der aufgerufenen Methode an der Aufrufstelle (bei <c>Begin{Node}(…)</c> bzw.
    /// <c>next.{Choice}(…)</c> der Methoden-Bezeichner selbst) — der Anker, an dem die Navigation ansetzt.
    /// </summary>
    [NotNull]
    public IdentifierNameSyntax Identifier { get; }
}