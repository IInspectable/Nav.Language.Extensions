#region Using Directives

using System;
using System.Collections.Generic;

using NDesk.Options;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language;

[Flags]
public enum CodeGenerationOptions {

    None        = 0x00,
    ToClasses   = 0x01,
    WflClasses  = 0x02,
    IwflClasses = 0x04,
    All         = ToClasses | WflClasses | IwflClasses,

}

sealed record CommandLine {

    // ReSharper disable once ConvertToPrimaryConstructor
    public CommandLine() {
        Sources           = new List<string>();
        GenerationOptions = CodeGenerationOptions.All;
    }

    public string       Directory            { get; private set; }
    public List<string> Sources              { get; }
    public bool         Force                { get; private set; }
    public bool         Strict               { get; private set; }
    public bool         UseSyntaxCache       { get; private set; }
    public bool         Verbose              { get; private set; }
    public bool         FullPaths            { get; private set; }
    public bool         NoWarnings           { get; private set; }
    public bool         NullableContext      { get; private set; }
    public string       ProjectRootDirectory { get; private set; }
    public string       IwflRootDirectory    { get; private set; }
    public string       WflRootDirectory     { get; private set; }
    public string       ManifestFile         { get; private set; }

    public CodeGenerationOptions GenerationOptions {get; private set;}

    public bool   Analyze { get; set; }
    public string Pattern { get; set; }
    
    public static CommandLine Parse(string[] commandline) {

        bool        showHelp = false;
        CommandLine cla      = new CommandLine();
        var p = new OptionSet {
            { "d=|directory="       , "Alle .nav-Dateien im Verzeichnis und allen Unterverzeichnissen sind Eingabedateien.", v => cla.Directory = v },
            { "s=|sources="         , ".nav-Eingabedatei.", v => cla.Sources.Add(v) },
            { "f|force"             , "Überschreibt die Ausgabedatei(en) auch wenn sich diese nicht geändert haben.", v => cla.Force = v!= null },
            { "t|strict"            , "Strikte Namespaces. In IWFL-Dateien werden z.B. nur Namespaces mit der Endung IWFL generiert.", v => cla.Strict = v != null },
            { "g=|genopts"          , $"Gibt an, welche Dateien generiert werden sollen ({GenerationOptionsString()}). Standardgemäß werden alle Dateien generiert.", v => cla.GenerationOptions = ParseGenerationOptions(v) },
            { "c|useSyntaxCache"    , "Cached Syntaxen an statt sie immer wieder neu zu parsen.", v => cla.UseSyntaxCache = v != null },
            { "nowarnings"          , "Unterdrückt Warnmeldungen in der Logausgabe.", v => cla.NoWarnings = v != null },
            { "v|verbose"           , "Schreibt ausführliche Meldungen in die Logausgabe.", v => cla.Verbose = v != null },
            { "fullpaths"           , "Wenn angegeben, werden in die Logausgaben ganze Pfade geschrieben.", v => cla.FullPaths = v != null },
            { "n|nullable"          , "Schreibt '#nullable enable' in die generierten Dateien (Nullable-Referenztyp-Kontext). Standardgemäß aus.", v => cla.NullableContext = v != null },
            { "i=|iwflroot"         , "Gibt ein alternatives IWFL Wurzelverzeichnis an.", v => cla.IwflRootDirectory = v },
            { "w=|wflroot"          , "Gibt ein alternatives WFL Wurzelverzeichnis an.", v => cla.WflRootDirectory = v },
            { "r=|projectroot"      , "Gibt das Project Wurzelverzeichnis an.", v => cla.ProjectRootDirectory = v },
            { "m=|manifest"         , "Schreibt die Liste aller erzeugten Ausgabedateien (Manifest) in die angegebene Datei — für inkrementelle Builds.", v => cla.ManifestFile = v },
            { "h|?|help"            , "Zeigt diese Hilfe an.", v => showHelp = v != null },

        };

        try {
            p.Parse(commandline);
        } catch (OptionException e) {
            Console.Error.WriteLine("nav.exe: ");
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine("Try 'nav.exe --help' for more information.");
            return null;
        }

        if (showHelp) {
            ShowHelp(p);
            return null;
        }

        return cla;          
    }

    static void ShowHelp(OptionSet p) {
        Console.WriteLine($"{MyAssembly.ProductName} v{MyAssembly.ProductVersion}");
        Console.WriteLine();
        Console.WriteLine("Usage: nav.exe [OPTIONS]+");            
        Console.WriteLine();
        Console.WriteLine("Options:");
        p.WriteOptionDescriptions(Console.Out);
    }

    static string GenerationOptionsString() {
        return string.Join(", ", Enum.GetNames(typeof(CodeGenerationOptions)));
    }

    static CodeGenerationOptions ParseGenerationOptions(string value) {
        if (value.IsNullOrEmpty()) {
            return CodeGenerationOptions.None;
        }

        return (CodeGenerationOptions)Enum.Parse(typeof(CodeGenerationOptions), value);
    }
}