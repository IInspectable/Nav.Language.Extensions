#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation; 

/// <summary>
/// Basisklasse aller Annotationen, die an einem generierten <em>Member</em> (einer Methode) hängen —
/// Init-, Exit-, Trigger- und Choice-Annotation. Ergänzt die von <see cref="NavTaskAnnotation"/>
/// geerbte Task-Herkunft um die konkrete <see cref="MethodDeclarationSyntax"/>, an der das Tag steht.
/// </summary>
public abstract class NavMethodAnnotation: NavTaskAnnotation {

    /// <summary>
    /// Übernimmt die Task-Herkunft aus <paramref name="taskAnnotation"/> und bindet die Annotation an die
    /// gegebene Methoden-Deklaration.
    /// </summary>
    /// <param name="taskAnnotation">Die Task-Annotation, deren Herkunft übernommen wird.</param>
    /// <param name="methodDeclarationSyntax">Die generierte Methode, an der das Nav-Tag hängt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="methodDeclarationSyntax"/> ist
    /// <see langword="null"/>.</exception>
    protected NavMethodAnnotation(NavTaskAnnotation taskAnnotation,
                                  MethodDeclarationSyntax methodDeclarationSyntax) : base(taskAnnotation) {

        MethodDeclarationSyntax = methodDeclarationSyntax ?? throw new ArgumentNullException(nameof(methodDeclarationSyntax));
    }

    /// <summary>
    /// Die generierte Methode, an der das Nav-Tag gefunden wurde — Anker für die Navigation in den
    /// generierten C#-Code.
    /// </summary>
    [NotNull]
    public MethodDeclarationSyntax MethodDeclarationSyntax { get; }
}