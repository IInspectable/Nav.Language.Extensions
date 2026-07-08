#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation; 

public partial class NavTaskAnnotation {

    public NavTaskAnnotation(ClassDeclarationSyntax classDeclarationSyntax, ClassDeclarationSyntax declaringClassDeclarationSyntax, string taskName, string navFileName) {

        ClassDeclarationSyntax          = classDeclarationSyntax          ?? throw new ArgumentNullException(nameof(classDeclarationSyntax));
        DeclaringClassDeclarationSyntax = declaringClassDeclarationSyntax ?? throw new ArgumentNullException(nameof(declaringClassDeclarationSyntax));
        TaskName                        = taskName                        ?? String.Empty;
        NavFileName                     = navFileName                     ?? String.Empty;
    }

    protected NavTaskAnnotation(NavTaskAnnotation other) {
        ClassDeclarationSyntax          = other?.ClassDeclarationSyntax ?? throw new ArgumentNullException(nameof(other));
        DeclaringClassDeclarationSyntax = other.DeclaringClassDeclarationSyntax;
        TaskName                        = other.TaskName;
        NavFileName                     = other.NavFileName;
    }

    [NotNull]
    public ClassDeclarationSyntax ClassDeclarationSyntax { get; }

    /// <summary>
    /// Liefert die Klasse, in der das Tag definiert wurde. Das kann und wird
    /// in vielen Fällen die Basisklasse "WFSBase" sein.
    /// </summary>
    [NotNull]
    public ClassDeclarationSyntax DeclaringClassDeclarationSyntax { get; }

    [NotNull]
    public string TaskName { get;}

    [NotNull]
    public string NavFileName { get; }
}