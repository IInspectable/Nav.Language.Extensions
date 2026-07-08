#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;

public partial class NavChoiceCallAnnotation: NavInvocationAnnotation {

    public NavChoiceCallAnnotation(NavTaskAnnotation taskAnnotation,
                                   IdentifierNameSyntax identifier,
                                   string choiceName,
                                   string wfsBaseFullyQualifiedName)
        : base(taskAnnotation, identifier) {

        ChoiceName               = choiceName ?? String.Empty;
        WfsBaseFullyQualifiedName = wfsBaseFullyQualifiedName ?? String.Empty;
    }

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
