#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation; 

/// <summary>
/// Annotation an einer generierten Trigger-Methode einer Task — der Rückverweis aus dem
/// <c>&lt;NavTrigger&gt;</c>-Tag auf den Trigger der Nav-Task.
/// </summary>
public partial class NavTriggerAnnotation: NavMethodAnnotation {

    /// <summary>
    /// Erzeugt die Trigger-Annotation aus dem am generierten Trigger-Member gefundenen Namen.
    /// </summary>
    /// <param name="taskAnnotation">Die Task-Annotation, deren Herkunft übernommen wird.</param>
    /// <param name="methodDeclaration">Die generierte Trigger-Methode, an der das Tag hängt.</param>
    /// <param name="triggerName">Der Name des Triggers (aus dem <c>&lt;NavTrigger&gt;</c>-Tag).</param>
    public NavTriggerAnnotation(NavTaskAnnotation taskAnnotation, 
                                MethodDeclarationSyntax methodDeclaration, 
                                string triggerName) : base(taskAnnotation, methodDeclaration) {

        TriggerName = triggerName ??String.Empty;
    }

    /// <summary>
    /// Der Name des Triggers, auf den die Annotation zurückverweist.
    /// </summary>
    [NotNull]
    public string TriggerName { get; }
}