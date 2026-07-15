#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;

/// <summary>
/// Annotation an einer generierten <c>{Choice}Logic</c>-Methode einer Task — der Rückverweis aus dem
/// <c>&lt;NavChoice&gt;</c>-Tag auf den Choice-Knoten der Nav-Task. Gegenstück zur aufrufseitigen
/// <see cref="NavChoiceCallAnnotation"/>: diese markiert die Choice-<em>Deklaration</em>, jene den
/// <c>{Choice}(…)</c>-Forward an der Aufrufstelle.
/// </summary>
public partial class NavChoiceAnnotation: NavMethodAnnotation {

    /// <summary>
    /// Erzeugt die Choice-Annotation aus dem an der generierten <c>{Choice}Logic</c>-Methode gefundenen Namen.
    /// </summary>
    /// <param name="taskAnnotation">Die Task-Annotation, deren Herkunft übernommen wird.</param>
    /// <param name="methodDeclaration">Die generierte <c>{Choice}Logic</c>-Methode, an der das Tag hängt.</param>
    /// <param name="choiceName">Der Name des Choice-Knotens (aus dem <c>&lt;NavChoice&gt;</c>-Tag).</param>
    public NavChoiceAnnotation(NavTaskAnnotation taskAnnotation,
                               MethodDeclarationSyntax methodDeclaration,
                               string choiceName) : base(taskAnnotation, methodDeclaration) {

        ChoiceName = choiceName ?? String.Empty;
    }

    /// <summary>
    /// Der Name des Choice-Knotens, auf den die Annotation zurückverweist.
    /// </summary>
    [NotNull]
    public string ChoiceName { get; }
}
