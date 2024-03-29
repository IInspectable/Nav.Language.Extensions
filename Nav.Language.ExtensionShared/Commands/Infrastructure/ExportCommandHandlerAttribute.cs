﻿#region Using Directives

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

[MetadataAttribute]
[AttributeUsage(AttributeTargets.Class)]
class ExportCommandHandlerAttribute : ExportAttribute {
    public string              Name         { get; }
    public IEnumerable<string> ContentTypes { get; }

    public ExportCommandHandlerAttribute(string name, params string[] contentTypes) :
        base(typeof(INavCommandHandler)) {
        Name         = name;
        ContentTypes = contentTypes;
    }
}