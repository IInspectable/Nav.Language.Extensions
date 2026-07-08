#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation; 

public partial class NavInitAnnotation : NavMethodAnnotation {

    public NavInitAnnotation(NavTaskAnnotation taskAnnotation,
                             MethodDeclarationSyntax methodDeclaration, 
                             string initName): base(taskAnnotation, methodDeclaration) {
        InitName = initName ?? String.Empty;
    }

    [NotNull]
    public string InitName { get; }
}