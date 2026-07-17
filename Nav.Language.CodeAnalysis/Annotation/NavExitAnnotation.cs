#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation; 

/// <summary>
/// Annotation an einer generierten Exit-Methode einer Task — der Rückverweis aus dem
/// <c>&lt;NavExit&gt;</c>-Tag auf den Exit-Verbindungspunkt der Nav-Task.
/// </summary>
public partial class NavExitAnnotation: NavMethodAnnotation {

    /// <summary>
    /// Erzeugt die Exit-Annotation aus dem am generierten Exit-Member gefundenen Namen.
    /// </summary>
    /// <param name="taskAnnotation">Die Task-Annotation, deren Herkunft übernommen wird.</param>
    /// <param name="methodDeclaration">Die generierte Exit-Methode, an der das Tag hängt.</param>
    /// <param name="exitTaskName">Der Name des Exit-Verbindungspunkts (aus dem <c>&lt;NavExit&gt;</c>-Tag).</param>
    public NavExitAnnotation(NavTaskAnnotation taskAnnotation, 
                             MethodDeclarationSyntax methodDeclaration, 
                             string exitTaskName): base(taskAnnotation, methodDeclaration) {
        ExitTaskName = exitTaskName ??String.Empty;
    }

    /// <summary>
    /// Der Name des Exit-Verbindungspunkts, auf den die Annotation zurückverweist.
    /// </summary>
    [NotNull]
    public string ExitTaskName { get;}
}