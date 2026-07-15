#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Dependencies; 

/// <summary>
/// Gruppiert eine flache Folge von <see cref="Dependency"/>-Kanten nach Endpunkt zu einer
/// Adjazenzabbildung — je Richtung eine Sicht (eingehend/ausgehend) für die Navigation im
/// Abhängigkeitsgraphen.
/// </summary>
public static class DependencyExtensions {
        
    /// <summary>
    /// Bildet je genutztem Element (<see cref="Dependency.UsedItem"/>) die Menge der auf es
    /// zeigenden Kanten ab — die eingehenden Abhängigkeiten („wer nutzt mich?").
    /// </summary>
    public static Dictionary<DependencyItem, List<Dependency>> CollectIncomingDependencies(this IEnumerable<Dependency> dependencies) {
        return Collect(dependencies, d => d.UsedItem, d => d);
    }

    /// <summary>
    /// Bildet je nutzendem Element (<see cref="Dependency.UsingItem"/>) die Menge der von ihm
    /// ausgehenden Kanten ab — die ausgehenden Abhängigkeiten („was nutze ich?").
    /// </summary>
    public static Dictionary<DependencyItem, List<Dependency>> CollectOutgoingDependencies(this IEnumerable<Dependency> dependencies) {
        return Collect(dependencies, d => d.UsingItem, d => d);
    }

    /// <summary>
    /// Gemeinsamer Gruppierungskern: schlägt je Kante über <paramref name="itemSelector"/> den
    /// Schlüssel-Endpunkt nach und hängt das über <paramref name="resultSelector"/> projizierte
    /// Ergebnis an dessen Liste an.
    /// </summary>
    static Dictionary<DependencyItem, List<T>> Collect<T>(IEnumerable<Dependency> dependencies,
                                                          Func<Dependency, DependencyItem> itemSelector, 
                                                          Func<Dependency, T> resultSelector) {

        var result = new Dictionary<DependencyItem, List<T>>();
        foreach (var d in dependencies) {
               
            DependencyItem key = itemSelector(d);
            if (!result.TryGetValue(key, out var list)) {
                result.Add(key, list = new List<T>());
            }
            list.Add(resultSelector(d));
        }
        return result;
    }
}