#region Using Directives

using System;
using System.IO;

using JetBrains.Annotations;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Utilities;

/// <summary>
/// Dünner Wrapper um eine <see cref="IVsHierarchy"/> des Visual-Studio-Projektsystems. Bündelt den
/// COM-Zugriff auf die Eigenschaften der Projekt-Wurzel (Dateipfad, Name, Projekt-GUID) hinter einer
/// typisierten Fassade und kapselt die dafür nötigen UI-Thread- und HRESULT-Prüfungen. Wird von
/// <see cref="ProjectService"/> beim Aufbau des <see cref="ProjectMapper"/> genutzt.
/// </summary>
readonly struct Hierarchy {

    private readonly IVsHierarchy _vsHierarchy;

    /// <summary>
    /// Erzeugt den Wrapper zur übergebenen <paramref name="vsHierarchy"/>.
    /// </summary>
    /// <param name="vsHierarchy">Die zu umhüllende Projekt-Hierarchie; darf nicht <c>null</c> sein.</param>
    public Hierarchy(IVsHierarchy vsHierarchy) {
        _vsHierarchy = vsHierarchy ?? throw new ArgumentNullException(nameof(vsHierarchy));

    }

    const uint ItemId = VSConstants.VSITEMID_ROOT;

    /// <summary>
    /// Der absolute (gerootete) Dateipfad der Projekt-Wurzel — bevorzugt aus
    /// <see cref="GetMkDocument"/>, ersatzweise aus <see cref="GetCanonicalName"/>. Liefert <c>null</c>,
    /// wenn kein absoluter Pfad ermittelt werden kann.
    /// </summary>
    public string FullPath {
        get {
            ThreadHelper.ThrowIfNotOnUIThread();

            var fullPath = GetMkDocument() ?? GetCanonicalName();

            if (!Path.IsPathRooted(fullPath)) {
                return null;
            }

            return fullPath;
        }
    }

    /// <summary>
    /// Fragt über <see cref="IVsProject.GetMkDocument"/> den Dokument-Moniker (Dateipfad) der
    /// Projekt-Wurzel ab. Liefert <c>null</c>, wenn die Hierarchie kein <see cref="IVsProject"/> ist
    /// oder keinen Moniker liefert.
    /// </summary>
    [CanBeNull]
    public string GetMkDocument() {

        ThreadHelper.ThrowIfNotOnUIThread();

        // ReSharper disable once SuspiciousTypeConversion.Global
        var    ao  = _vsHierarchy as IVsProject;
        string doc = null;
        ao?.GetMkDocument(ItemId, out doc);
        return doc;
    }

    /// <summary>
    /// Fragt über <see cref="IVsHierarchy.GetCanonicalName"/> den kanonischen Namen der Projekt-Wurzel
    /// ab (bei Projekten üblicherweise der Dateipfad). Liefert <c>null</c>, wenn der Aufruf fehlschlägt.
    /// </summary>
    [CanBeNull]
    public string GetCanonicalName() {

        ThreadHelper.ThrowIfNotOnUIThread();

        string cn = null;
        if (ErrorHandler.Succeeded(_vsHierarchy?.GetCanonicalName(ItemId, out cn) ?? VSConstants.S_OK)) {
            return cn;
        }

        return null;
    }

    /// <summary>
    /// Der Anzeigename des Projekts (VS-Property <see cref="__VSHPROPID.VSHPROPID_Name"/>).
    /// </summary>
    public string Name => GetProperty<string>(__VSHPROPID.VSHPROPID_Name);

    /// <summary>
    /// Die eindeutige Projekt-GUID (VS-Property <see cref="__VSHPROPID.VSHPROPID_ProjectIDGuid"/>).
    /// </summary>
    public Guid ProjectGuid {
        get {
            ThreadHelper.ThrowIfNotOnUIThread();

            _vsHierarchy.GetGuidProperty(ItemId, (int) __VSHPROPID.VSHPROPID_ProjectIDGuid, out var projectGuid);
               
            return projectGuid;
        }
    }

    /// <summary>
    /// Liest die Hierarchie-Eigenschaft <paramref name="propId"/> und castet sie auf <typeparamref name="T"/>;
    /// bei fehlendem Wert wird <paramref name="defaultValue"/> zurückgegeben.
    /// </summary>
    /// <typeparam name="T">Erwarteter Laufzeittyp der Eigenschaft.</typeparam>
    /// <param name="propId">Die abzufragende VS-Hierarchie-Eigenschaft.</param>
    /// <param name="defaultValue">Rückgabewert, falls die Eigenschaft keinen Wert liefert.</param>
    T GetProperty<T>(__VSHPROPID propId, T defaultValue = default) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var value = GetPropertyCore((int) propId);
        if (value == null) {
            return defaultValue;
        }

        return (T) value;
    }

    /// <summary>
    /// Roher <see cref="IVsHierarchy.GetProperty"/>-Zugriff auf die Projekt-Wurzel; liefert <c>null</c>
    /// für <see cref="__VSHPROPID.VSHPROPID_NIL"/> bzw. eine fehlende Eigenschaft.
    /// </summary>
    /// <param name="propId">Die numerische ID der abzufragenden VS-Hierarchie-Eigenschaft.</param>
    object GetPropertyCore(int propId) {

        ThreadHelper.ThrowIfNotOnUIThread();

        if (propId == (int) __VSHPROPID.VSHPROPID_NIL) {
            return null;
        }

        _vsHierarchy.GetProperty(ItemId, propId, out object propValue);

        return propValue;
    }

}