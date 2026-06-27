#region Using Directives

using System;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

#endregion

namespace Pharmatechnik.Nav.Language.Extension; 

partial class NavSolutionProvider: IVsHierarchyEvents {

    private uint _hierarchyEventsCookie;

    bool AreHierarchyEventsConnected => _hierarchyEventsCookie != 0;

    void ConnectHierarchyEvents() {

        ThreadHelper.ThrowIfNotOnUIThread();

        if (!AreHierarchyEventsConnected) {

            GetSolutionHierarchy()?.AdviseHierarchyEvents(this, out _hierarchyEventsCookie);
        }

    }

    IVsHierarchy GetSolutionHierarchy() {
        ThreadHelper.ThrowIfNotOnUIThread();

        var vsSolution = (IVsSolution) ServiceProvider.GetService(typeof(SVsSolution)) ?? throw new InvalidOperationException();

        // ReSharper disable once SuspiciousTypeConversion.Global
        return vsSolution as IVsHierarchy;
    }

    // TODO DisconnectHierarchyEvents beim Beenden von Studio
    // ReSharper disable once UnusedMember.Local
    void DisconnectHierarchyEvents() {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (AreHierarchyEventsConnected) {
            GetSolutionHierarchy().UnadviseHierarchyEvents(_hierarchyEventsCookie);
            _hierarchyEventsCookie = 0;
        }
    }

    int IVsHierarchyEvents.OnItemAdded(uint itemidParent, uint itemidSiblingPrev, uint itemidAdded) {
        return VSConstants.S_OK;
    }

    int IVsHierarchyEvents.OnItemsAppended(uint itemidParent) {
        return VSConstants.S_OK;
    }

    int IVsHierarchyEvents.OnItemDeleted(uint itemid) {
        return VSConstants.S_OK;
    }

    int IVsHierarchyEvents.OnPropertyChanged(uint itemid, int propid, uint flags) {

        if (propid == (int) __VSHPROPID.VSHPROPID_ProjectName &&
            itemid == (uint) VSConstants.VSITEMID.Root) {

            Invalidate();
        }

        return VSConstants.S_OK;
    }

    int IVsHierarchyEvents.OnInvalidateItems(uint itemidParent) {
        return VSConstants.S_OK;
    }

    int IVsHierarchyEvents.OnInvalidateIcon(IntPtr hicon) {
        return VSConstants.S_OK;
    }

}