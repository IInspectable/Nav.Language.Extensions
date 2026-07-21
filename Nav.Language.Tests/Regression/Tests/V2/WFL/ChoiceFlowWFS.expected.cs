#region Using Directives
using System;
using Nav.Language.Tests.Regression.V2.Choice.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.WFL;
using Pharmatechnik.Apotheke.XTplus.Framework.NavigationEngine.IWFL;
#endregion

namespace Nav.Language.Tests.Regression.V2.Choice.WFL;

public partial class ChoiceFlowWFS {
    protected override Init1CallContext.Result BeginLogic(string message,
                                                          Init1CallContext next) {
        throw new NotImplementedException();
    }

    protected override AfterACallContext.Result AfterALogic(AResult result,
                                                            AfterACallContext next) {
        throw new NotImplementedException();
    }

    protected override AfterMsgCallContext.Result AfterMsgLogic(MsgResult result,
                                                                AfterMsgCallContext next) {
        throw new NotImplementedException();
    }

    protected override OnRetryCallContext.Result OnRetryLogic(HomeTO to,
                                                              OnRetryCallContext next) {
        throw new NotImplementedException();
    }

    protected override OnStartACallContext.Result OnStartALogic(HomeTO to,
                                                                OnStartACallContext next) {
        throw new NotImplementedException();
    }

    protected override Choice_RetryCallContext.Result Choice_RetryLogic(string reason,
                                                                        Choice_RetryCallContext next) {
        throw new NotImplementedException();
    }

    protected override Choice_EscalateCallContext.Result Choice_EscalateLogic(int level,
                                                                              Choice_EscalateCallContext next) {
        throw new NotImplementedException();
    }
}