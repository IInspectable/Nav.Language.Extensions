#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation; 

/// <summary>
/// Annotation an der generierten Init-Methode einer Task — der Rückverweis aus dem <c>&lt;NavInit&gt;</c>-Tag
/// auf den Init-Verbindungspunkt der Nav-Task.
/// </summary>
public partial class NavInitAnnotation : NavMethodAnnotation {

    /// <summary>
    /// Erzeugt die Init-Annotation aus dem am generierten Init-Member gefundenen Namen.
    /// </summary>
    /// <param name="taskAnnotation">Die Task-Annotation, deren Herkunft übernommen wird.</param>
    /// <param name="methodDeclaration">Die generierte Init-Methode, an der das Tag hängt.</param>
    /// <param name="initName">Der Name des Init-Verbindungspunkts (aus dem <c>&lt;NavInit&gt;</c>-Tag).</param>
    public NavInitAnnotation(NavTaskAnnotation taskAnnotation,
                             MethodDeclarationSyntax methodDeclaration, 
                             string initName): base(taskAnnotation, methodDeclaration) {
        InitName = initName ?? String.Empty;
    }

    /// <summary>
    /// Der Name des Init-Verbindungspunkts, auf den die Annotation zurückverweist.
    /// </summary>
    [NotNull]
    public string InitName { get; }
}