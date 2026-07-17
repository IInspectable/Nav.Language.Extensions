#region Using Directives

using System.Threading;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

/// <summary>
/// Ein Anbieter von Lightbulb-Aktionen für eine Fix-Familie. Jeder Provider brückt eine Gruppe von
/// Engine-Fixes: Er lässt die Engine für einen <see cref="CodeFixSuggestedActionParameter"/> die passenden
/// Fixes berechnen und verpackt sie als <see cref="CodeFixSuggestedAction"/>en. Implementierungen werden
/// per <see cref="ExportCodeFixSuggestedActionProviderAttribute"/> exportiert und vom
/// <see cref="CodeFixSuggestedActionProviderService"/> eingesammelt.
/// </summary>
interface ICodeFixSuggestedActionProvider {
    /// <summary>Berechnet die für den Parameter passenden Lightbulb-Aktionen.</summary>
    /// <param name="parameter">Der aufruf-spezifische Eingabe-Zustand (Bereich, Snapshot, TextView).</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Berechnung.</param>
    /// <returns>Die angebotenen Aktionen (ggf. leer).</returns>
    IEnumerable<CodeFixSuggestedAction> GetSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken);
}

/// <summary>
/// Die abstrakte Basis der <see cref="ICodeFixSuggestedActionProvider"/>-Implementierungen. Sie hält den
/// per MEF injizierten, geteilten <see cref="CodeFixSuggestedActionContext"/>, den die Ableitung in die
/// erzeugten Aktionen weiterreicht.
/// </summary>
abstract class CodeFixSuggestedActionProvider : ICodeFixSuggestedActionProvider {

    /// <summary>Initialisiert die Basis mit dem geteilten Dienst-Kontext.</summary>
    /// <param name="context">Der geteilte Kontext (Wait-Indicator, <see cref="ITextChangeService"/>, Dialog-Dienst).</param>
    protected CodeFixSuggestedActionProvider(CodeFixSuggestedActionContext context) {
        Context = context;
    }

    /// <summary>Der geteilte Dienst-Kontext, den die Ableitung in ihre Aktionen weiterreicht.</summary>
    protected CodeFixSuggestedActionContext Context { get; }

    /// <inheritdoc/>
    public abstract IEnumerable<CodeFixSuggestedAction> GetSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken);
}