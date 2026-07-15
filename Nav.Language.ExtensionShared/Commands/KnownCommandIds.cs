using System;
using System.ComponentModel.Design;

using Microsoft.VisualStudio;

namespace Pharmatechnik.Nav.Language.Extension.Commands;

/// <summary>
/// Bekannte Visual-Studio-<see cref="CommandID"/>s, die die Nav-Extension abfragt oder auslöst — jede
/// besteht aus dem Command-Set (GUID) und der numerischen Command-ID. Bündelt sowohl Standard-Shell-Befehle
/// (<see cref="ViewCode"/>, <see cref="FormatDocument"/>) als auch den hauseigenen Codegen-Befehl aus dem
/// IXOS-Essentials-Paket (<see cref="NavGenerateCommand"/>).
/// </summary>
static class KnownCommandIds {

    /// <summary>GUID des IXOS-Essentials-Command-Sets als String (siehe <see cref="GuidIXOSEssentialsPackageCmdSet"/>).</summary>
    public const  string GuidIXOSEssentialsPackageCmdSetString = "6b794c4c-4923-45f3-a677-8cfca59df62f";
    /// <summary>Command-Set-<see cref="Guid"/> des IXOS-Essentials-Pakets, aus dem der Nav-Codegen-Befehl stammt.</summary>
    public static Guid   GuidIXOSEssentialsPackageCmdSet       = new(GuidIXOSEssentialsPackageCmdSetString);

    /// <summary>Standard-Shell-Befehl „View Code" (<see cref="VSConstants.VSStd97CmdID.ViewCode"/>) — Sprung in den generierten C#-Code.</summary>
    public static readonly CommandID ViewCode           = new(new Guid("{5efc7975-14bc-11cf-9b2b-00aa00573819}"), (int)VSConstants.VSStd97CmdID.ViewCode);
    /// <summary>Nav-Codegen-Befehl (Command-ID <c>0x0240</c>) aus dem IXOS-Essentials-Paket.</summary>
    public static readonly CommandID NavGenerateCommand = new(GuidIXOSEssentialsPackageCmdSet, 0x0240);
    /// <summary>Standard-Editor-Befehl „Format Document" (<see cref="VSConstants.VSStd2KCmdID.FORMATDOCUMENT"/>).</summary>
    public static readonly CommandID FormatDocument     = new(VSConstants.VSStd2K, (int)VSConstants.VSStd2KCmdID.FORMATDOCUMENT);

}