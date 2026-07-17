namespace Pharmatechnik.Nav.Language.Dependencies;

/// <summary>
/// Eine gerichtete Abhängigkeitskante zwischen zwei <see cref="DependencyItem"/>: <see cref="UsingItem"/>
/// hängt von <see cref="UsedItem"/> ab (nutzt es). Erzeugt von <see cref="DependencyAnalyzer"/> beim
/// Sammeln der Task-Aufrufbeziehungen und über <see cref="DependencyExtensions"/> nach ein-/ausgehenden
/// Kanten gruppierbar.
/// </summary>
public sealed class Dependency {
        
    /// <summary>
    /// Erzeugt eine Abhängigkeitskante von <paramref name="usingItem"/> (der abhängigen Seite) auf
    /// <paramref name="usedItem"/> (der genutzten Seite).
    /// </summary>
    /// <param name="usingItem">Das nutzende Element — die Quelle der Kante.</param>
    /// <param name="usedItem">Das genutzte Element — das Ziel der Kante.</param>
    public Dependency(DependencyItem usingItem, DependencyItem usedItem) {
        UsingItem = usingItem;
        UsedItem  = usedItem;
    }

    /// <summary>Das nutzende (abhängige) Element — die Quelle der gerichteten Kante.</summary>
    public DependencyItem UsingItem { get; }
    /// <summary>Das genutzte Element, von dem <see cref="UsingItem"/> abhängt — das Ziel der Kante.</summary>
    public DependencyItem UsedItem  { get; }
}