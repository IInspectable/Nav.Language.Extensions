#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation; 

public abstract class NavMethodAnnotation: NavTaskAnnotation {

    protected NavMethodAnnotation(NavTaskAnnotation taskAnnotation,
                                  MethodDeclarationSyntax methodDeclarationSyntax) : base(taskAnnotation) {

        MethodDeclarationSyntax = methodDeclarationSyntax ?? throw new ArgumentNullException(nameof(methodDeclarationSyntax));
    }

    [NotNull]
    public MethodDeclarationSyntax MethodDeclarationSyntax { get; }
}