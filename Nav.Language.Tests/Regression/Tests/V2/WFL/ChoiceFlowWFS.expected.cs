#region Using Directives
using System;
using Nav.Language.Tests.Regression.V2.Choice.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL;
#endregion

namespace Nav.Language.Tests.Regression.V2.Choice.WFL {
    public partial class ChoiceFlowWFS {
        protected override Init1CallContext.Result BeginLogic(string message,
                                                              Init1CallContext callContext) {
            throw new NotImplementedException();
        }

        protected override AfterACallContext.Result AfterALogic(AResult result,
                                                                AfterACallContext callContext) {
            throw new NotImplementedException();
        }

        protected override AfterMsgCallContext.Result AfterMsgLogic(MsgResult result,
                                                                    AfterMsgCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override OnRetryCallContext.Result OnRetryLogic(HomeTO to,
                                                                  OnRetryCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override OnStartACallContext.Result OnStartALogic(HomeTO to,
                                                                    OnStartACallContext callContext) {
            throw new NotImplementedException();
        }

        protected override Choice_RetryCallContext.Result Choice_RetryLogic(string reason,
                                                                            Choice_RetryCallContext callContext) {
            throw new NotImplementedException();
        }

        protected override Choice_EscalateCallContext.Result Choice_EscalateLogic(int level,
                                                                                  Choice_EscalateCallContext callContext) {
            throw new NotImplementedException();
        }
    }
}