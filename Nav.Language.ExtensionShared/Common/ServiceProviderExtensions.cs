using System;

using Microsoft.VisualStudio.ComponentModelHost;

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Erweiterungsmethoden für den <see cref="IServiceProvider"/> von Visual Studio.
/// </summary>
static class ServiceProviderExtensions {

    /// <summary>
    /// Löst einen MEF-Dienst über das VS-Komponentenmodell (<c>SComponentModel</c>) auf.
    /// </summary>
    /// <typeparam name="T">Der aufzulösende Dienst-Typ.</typeparam>
    /// <param name="serviceProvider">Der VS-Dienst-Provider.</param>
    /// <returns>Der Dienst oder <c>null</c>, wenn das Komponentenmodell nicht verfügbar ist.</returns>
    public static T GetMefService<T>(this IServiceProvider serviceProvider) where T : class {

        var componentModel = serviceProvider.GetService(typeof(SComponentModel)) as IComponentModel;
        return componentModel?.GetService<T>();
    }

}