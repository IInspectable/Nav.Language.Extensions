#region Using Directives
using System;
using Nav.Language.Tests.Regression.V2.Cont.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL;
#endregion

namespace Nav.Language.Tests.Regression.V2.Cont.WFL {
    public partial class ContinuationFlowWFS {
        protected override Init1CallContext.Result BeginLogic(Init1CallContext callContext) {
            throw new NotImplementedException();
        }

        protected override AfterWarnCallContext.Result AfterWarnLogic(MsgResult result,
                                                                      AfterWarnCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override AfterDrillCallContext.Result AfterDrillLogic(DetailResult result,
                                                                        AfterDrillCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override OnCloseCallContext.Result OnCloseLogic(HomeTO to,
                                                                  OnCloseCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override OnShowWarnCallContext.Result OnShowWarnLogic(HomeTO to,
                                                                        OnShowWarnCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override OnDrillDownCallContext.Result OnDrillDownLogic(HomeTO to,
                                                                          OnDrillDownCallContext callContext) {
            throw new NotImplementedException();
        }
    }
}