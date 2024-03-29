﻿using System.IO;
// ReSharper disable InconsistentNaming
// ReSharper disable AssignNullToNotNullAttribute

namespace Nav.Language.Tests; 

static class Resources {
    public static readonly string FrameworkStubsCode      = LoadText("FrameworkStubs.cs");
    public static readonly string TaskA                   = LoadText("TaskA.nav");
    public static readonly string TaskB                   = LoadText("TaskB.nav");
    public static readonly string TaskC                   = LoadText("TaskC.nav");
    public static readonly string AllRules                = LoadText("AllRules.nav");
    public static readonly string LargeNav                = LoadText("LargeNav.nav");
    public static readonly string NavWithError            = LoadText("NavWithError.nav");
    public static readonly string SingleFileNav           = LoadText("SingleFile.nav");
    public static readonly string NestedChoices           = LoadText("NestedChoices.nav");
    public static readonly string FrameworkStubsWithoutNS = LoadText("FrameworkStubsWithoutNS.cs");
        
    //NavWithError
    static string LoadText(string resourceName) {

        var fullResourceName = $"{typeof(Resources).Namespace}.Resources.{resourceName}";

        using Stream       stream = typeof(Resources).Assembly.GetManifestResourceStream(fullResourceName);
        using StreamReader reader = new StreamReader(stream);
        string             result = reader.ReadToEnd();
        return result;

    }
}