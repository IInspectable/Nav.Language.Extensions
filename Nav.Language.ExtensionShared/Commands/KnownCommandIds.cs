using System;
using System.ComponentModel.Design;

using Microsoft.VisualStudio;

namespace Pharmatechnik.Nav.Language.Extension.Commands;

static class KnownCommandIds {

    public const  string GuidIXOSEssentialsPackageCmdSetString = "6b794c4c-4923-45f3-a677-8cfca59df62f";
    public static Guid   GuidIXOSEssentialsPackageCmdSet       = new(GuidIXOSEssentialsPackageCmdSetString);

    public static readonly CommandID ViewCode           = new(new Guid("{5efc7975-14bc-11cf-9b2b-00aa00573819}"), (int)VSConstants.VSStd97CmdID.ViewCode);
    public static readonly CommandID NavGenerateCommand = new(GuidIXOSEssentialsPackageCmdSet, 0x0240);
    public static readonly CommandID FormatDocument     = new(VSConstants.VSStd2K, (int)VSConstants.VSStd2KCmdID.FORMATDOCUMENT);

}