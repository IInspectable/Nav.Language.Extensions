#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation; 

public partial class NavTriggerAnnotation: NavMethodAnnotation {

    public NavTriggerAnnotation(NavTaskAnnotation taskAnnotation, 
                                MethodDeclarationSyntax methodDeclaration, 
                                string triggerName) : base(taskAnnotation, methodDeclaration) {

        TriggerName = triggerName ??String.Empty;
    }

    [NotNull]
    public string TriggerName { get; }
}