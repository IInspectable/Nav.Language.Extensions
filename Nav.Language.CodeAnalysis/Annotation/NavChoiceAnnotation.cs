#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;

public partial class NavChoiceAnnotation: NavMethodAnnotation {

    public NavChoiceAnnotation(NavTaskAnnotation taskAnnotation,
                               MethodDeclarationSyntax methodDeclaration,
                               string choiceName) : base(taskAnnotation, methodDeclaration) {

        ChoiceName = choiceName ?? String.Empty;
    }

    [NotNull]
    public string ChoiceName { get; }
}
