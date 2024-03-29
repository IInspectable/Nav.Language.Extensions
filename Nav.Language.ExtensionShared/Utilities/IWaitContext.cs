﻿using System;
using System.Threading;

namespace Pharmatechnik.Nav.Language.Extension.Utilities; 

enum WaitIndicatorResult {

    Completed,
    Canceled,

}

interface IWaitContext : IDisposable {

    CancellationToken CancellationToken { get; }

    bool   AllowCancel { get; set; }
    string Message     { get; set; }

    void UpdateProgress();

}