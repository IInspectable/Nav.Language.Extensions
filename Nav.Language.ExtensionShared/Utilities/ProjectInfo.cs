﻿#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Utilities;

readonly struct ProjectInfo {

    private readonly string _name;

    public ProjectInfo(Uri directory, string name, Guid projectGuid) {

        _name            = name      ?? throw new ArgumentNullException(nameof(name));
        ProjectDirectory = directory ?? throw new ArgumentNullException(nameof(directory));
        ProjectGuid      = projectGuid;
    }

    public string ProjectName      => _name ?? ProjectMapper.MiscellaneousFiles;
    public Uri    ProjectDirectory { get; }
    public Guid   ProjectGuid      { get; }

}