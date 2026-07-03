#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Dependencies; 

public static class DependencyExtensions {
        
    public static Dictionary<DependencyItem, List<Dependency>> CollectIncomingDependencies(this IEnumerable<Dependency> dependencies) {
        return Collect(dependencies, d => d.UsedItem, d => d);
    }

    public static Dictionary<DependencyItem, List<Dependency>> CollectOutgoingDependencies(this IEnumerable<Dependency> dependencies) {
        return Collect(dependencies, d => d.UsingItem, d => d);
    }

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