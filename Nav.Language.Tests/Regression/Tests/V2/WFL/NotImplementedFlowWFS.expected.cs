#region Using Directives
using System;
using Nav.Language.Tests.Regression.V2.NotImpl.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL;
#endregion

namespace Nav.Language.Tests.Regression.V2.NotImpl.WFL {
    public partial class NotImplementedFlowWFS {
        protected override Init1CallContext.Result BeginLogic(Init1CallContext callContext) {
            throw new NotImplementedException();
        }

        protected override OnWarnCallContext.Result OnWarnLogic(HomeTO to,
                                                                OnWarnCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override OnCloseCallContext.Result OnCloseLogic(HomeTO to,
                                                                  OnCloseCallContext callContext) {
            throw new NotImplementedException();
        }
    }
}