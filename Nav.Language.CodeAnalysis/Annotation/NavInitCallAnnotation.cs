#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Annotation; 

/// <summary>
/// Annotation an einem generierten Init-Aufruf (<c>Begin{Node}(…)</c> bzw. <c>ctx.Begin{Node}(…)</c>) —
/// der Rückverweis aus dem <c>&lt;NavInitCall&gt;</c>-Tag der aufgerufenen Begin-Methode auf den
/// zugehörigen Init-Verbindungspunkt der aufgerufenen Sub-Task. Verortet damit die Aufrufstelle im
/// generierten C#-Code als Sprungziel des Init-Aufrufs.
/// </summary>
public partial class NavInitCallAnnotation: NavInvocationAnnotation {

    /// <summary>
    /// Erzeugt die Init-Aufruf-Annotation aus dem am aufgerufenen Begin-Member gefundenen Tag.
    /// </summary>
    /// <param name="taskAnnotation">Die Task-Annotation der aufrufenden Task, deren Herkunft übernommen wird.</param>
    /// <param name="identifier">Der Methoden-Bezeichner an der Aufrufstelle — der navigierbare Anker.</param>
    /// <param name="beginItfFullyQualifiedName">Der voll qualifizierte Name der Begin-Schnittstelle des
    /// Init-Verbindungspunkts (aus dem <c>&lt;NavInitCall&gt;</c>-Tag).</param>
    /// <param name="parameter">Die Parametertypen des aufgerufenen Begin-Members, geordnet nach Position —
    /// zur Unterscheidung überladener Begin-Aufrufe.</param>
    public NavInitCallAnnotation(NavTaskAnnotation taskAnnotation, 
                                 IdentifierNameSyntax identifier, 
                                 string beginItfFullyQualifiedName, 
                                 List<string> parameter)
        : base(taskAnnotation, identifier) {

        BeginItfFullyQualifiedName = beginItfFullyQualifiedName ?? String.Empty;
        Parameter                  = (parameter ?? new List<string>()).ToImmutableList();
    }

    /// <summary>
    /// Der voll qualifizierte Name der Begin-Schnittstelle des angesprochenen Init-Verbindungspunkts —
    /// die Brücke, über die der Init-Aufruf auf den <c>init</c>-Knoten der aufgerufenen Task aufgelöst wird.
    /// </summary>
    [NotNull]
    public string BeginItfFullyQualifiedName { get;}

    /// <summary>
    /// Die nach Position geordneten Parametertypen des aufgerufenen Begin-Members — dient dazu, bei
    /// mehreren gleichnamigen Begin-Überladungen die passende zu bestimmen.
    /// </summary>
    [NotNull]
    public ImmutableList<string> Parameter { get; }
}