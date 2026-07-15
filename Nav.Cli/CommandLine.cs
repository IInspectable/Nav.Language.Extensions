#region Using Directives

using System;
using System.Collections.Generic;

using NDesk.Options;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Bitmaske der Artefakt-Klassen, die der <see cref="Generator.NavCodeGenerator"/> aus den <c>.nav</c>-Eingaben
/// erzeugen soll — die Host-seitige Entsprechung der Engine-<see cref="CodeGen.GenerationOptions"/>. Wird über
/// die CLI-Option <c>-g|genopts</c> gesetzt (vgl. <see cref="CommandLine.GenerationOptions"/>) und in
/// <see cref="Generator.NavCodeGenerator"/> in die Engine-<see cref="CodeGen.GenerationOptions"/> übersetzt.
/// </summary>
[Flags]
public enum CodeGenerationOptions {

    /// <summary>Kein Artefakt — es wird nichts generiert.</summary>
    None        = 0x00,
    /// <summary>
    /// Die TO-Klassen (Transfer-Objekte). Bewusst <c>opt-in</c> und daher <b>nicht</b> Teil von
    /// <see cref="All"/> (vgl. <see cref="CodeGen.GenerationOptions.GenerateToClasses"/>).
    /// </summary>
    ToClasses   = 0x01,
    /// <summary>Die WFL-Klassen (Workflow-Logik, vgl. <see cref="CodeGen.GenerationOptions.GenerateWflClasses"/>).</summary>
    WflClasses  = 0x02,
    /// <summary>Die IWFL-Klassen (Interface-Schicht der Workflow-Logik, vgl. <see cref="CodeGen.GenerationOptions.GenerateIwflClasses"/>).</summary>
    IwflClasses = 0x04,
    // TO-Klassen sind bewusst opt-in und NICHT Teil von All (siehe GenerationOptions.GenerateToClasses).
    /// <summary>
    /// Der Standardumfang: <see cref="WflClasses"/> und <see cref="IwflClasses"/>. <see cref="ToClasses"/>
    /// ist bewusst nicht enthalten und muss separat angefordert werden.
    /// </summary>
    All         = WflClasses | IwflClasses,

}

/// <summary>
/// Das per <see cref="OptionSet">NDesk.Options</see> geparste Options-Modell des CLI-Hosts: eine
/// Property je Kommandozeilen-Option. <see cref="Parse"/> erzeugt und befüllt die Instanz aus dem
/// Argumentvektor; <see cref="Program"/> liest daraus die Weiche (Generieren vs. Analysieren) und reicht
/// das Modell an <see cref="Generator.NavCodeGenerator"/> bzw. <see cref="Analyzer.SyntaxAnalyzerProgram"/> weiter.
/// </summary>
sealed record CommandLine {

    // ReSharper disable once ConvertToPrimaryConstructor
    /// <summary>
    /// Initialisiert die Standardwerte: leere <see cref="Sources"/>-Liste und der Voll-Umfang
    /// <see cref="CodeGenerationOptions.All"/> für <see cref="CodeGen.GenerationOptions"/>.
    /// </summary>
    public CommandLine() {
        Sources           = new List<string>();
        GenerationOptions = CodeGenerationOptions.All;
    }

    /// <summary>
    /// Das per <c>-d|directory</c> angegebene Wurzelverzeichnis; alle <c>.nav</c>-Dateien darin und in
    /// allen Unterverzeichnissen sind Eingaben. Alternative zum dateiweisen <see cref="Sources"/>.
    /// </summary>
    public string       Directory              { get; private set; }
    /// <summary>Die per <c>-s|sources</c> einzeln angegebenen <c>.nav</c>-Eingabedateien.</summary>
    public List<string> Sources                { get; }
    /// <summary>
    /// <c>-f|force</c>: Ausgabedatei(en) auch dann überschreiben, wenn sie sich inhaltlich nicht
    /// geändert haben.
    /// </summary>
    public bool         Force                  { get; private set; }
    /// <summary>
    /// <c>-t|strict</c>: strikte Namespaces — in IWFL-Dateien werden z.B. nur Namespaces mit der Endung
    /// IWFL generiert.
    /// </summary>
    public bool         Strict                 { get; private set; }
    /// <summary><c>-c|useSyntaxCache</c>: Syntaxen zwischenspeichern, statt sie wiederholt neu zu parsen.</summary>
    public bool         UseSyntaxCache         { get; private set; }
    /// <summary><c>-v|verbose</c>: ausführliche Meldungen in die Logausgabe schreiben.</summary>
    public bool         Verbose                { get; private set; }
    /// <summary><c>-fullpaths</c>: in der Logausgabe vollständige Pfade statt Kurzformen ausgeben.</summary>
    public bool         FullPaths              { get; private set; }
    /// <summary><c>-nowarnings</c>: Warnmeldungen in der Logausgabe unterdrücken.</summary>
    public bool         NoWarnings             { get; private set; }
    /// <summary>
    /// <c>-n|nullable</c>: <c>#nullable enable</c> in die generierten Dateien schreiben
    /// (Nullable-Referenztyp-Kontext). Standardmäßig aus.
    /// </summary>
    public bool         NullableContext        { get; private set; }
    /// <summary>
    /// <c>-r|projectroot</c>: das Projekt-Wurzelverzeichnis, gegen das <see cref="Generator.NavCodeGenerator"/> die
    /// Ausgabe-Wurzeln validiert.
    /// </summary>
    public string       ProjectRootDirectory   { get; private set; }
    /// <summary><c>-i|iwflroot</c>: alternatives IWFL-Wurzelverzeichnis.</summary>
    public string       IwflRootDirectory      { get; private set; }
    /// <summary><c>-w|wflroot</c>: alternatives WFL-Wurzelverzeichnis.</summary>
    public string       WflRootDirectory       { get; private set; }
    /// <summary>
    /// <c>-m|manifest</c>: Zieldatei für die Liste aller erzeugten Ausgabedateien (Manifest) — Grundlage
    /// der inkrementellen Builds.
    /// </summary>
    public string       ManifestFile           { get; private set; }
    /// <summary>
    /// <c>-dm|depsmanifest</c>: Zieldatei für die Liste aller per <c>taskref</c> eingelesenen
    /// Abhängigkeitsdateien (Abhängigkeits-Manifest) — Grundlage der inkrementellen Builds.
    /// </summary>
    public string       DependencyManifestFile { get; private set; }

    /// <summary>
    /// <c>-g|genopts</c>: welche Artefakt-Klassen erzeugt werden (siehe <see cref="CodeGenerationOptions"/>).
    /// Standard ist <see cref="CodeGenerationOptions.All"/>.
    /// </summary>
    public CodeGenerationOptions GenerationOptions { get; private set; }

    /// <summary>
    /// Schaltet vom Generier- auf den Analyse-Pfad um: ist <see langword="true"/>, fährt
    /// <see cref="Program"/> den <see cref="Analyzer.SyntaxAnalyzerProgram"/> statt <see cref="Generator.NavCodeGenerator"/>.
    /// </summary>
    public bool   Analyze { get; set; }
    /// <summary>Das Suchmuster des Analyse-Pfads (vgl. <see cref="Analyzer.SyntaxAnalyzerProgram"/>).</summary>
    public string Pattern { get; set; }
    
    /// <summary>
    /// Parst den Argumentvektor über ein <see cref="OptionSet"/> in ein <see cref="CommandLine"/>-Modell.
    /// </summary>
    /// <param name="commandline">Die (bereits um ein etwaiges Response-File aufgelösten) Argumente.</param>
    /// <returns>
    /// Das befüllte Modell; <see langword="null"/>, wenn eine <see cref="OptionException"/> auftrat oder
    /// die Hilfe angefordert wurde (<c>-h|?|help</c>) — in beiden Fällen soll der Host beenden.
    /// </returns>
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
            { "dm=|depsmanifest"    , "Schreibt die Liste aller per taskref eingelesenen Abhängigkeitsdateien in die angegebene Datei — für inkrementelle Builds.", v => cla.DependencyManifestFile = v },
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

    /// <summary>Gibt den Verwendungshinweis und die Optionsbeschreibungen des Hosts aus.</summary>
    /// <param name="p">Das <see cref="OptionSet"/>, dessen Optionen beschrieben werden.</param>
    static void ShowHelp(OptionSet p) {
        Console.WriteLine($"{MyAssembly.ProductName} v{MyAssembly.ProductVersion}");
        Console.WriteLine();
        Console.WriteLine("Usage: nav.exe [OPTIONS]+");            
        Console.WriteLine();
        Console.WriteLine("Options:");
        p.WriteOptionDescriptions(Console.Out);
    }

    /// <summary>Liefert die kommaseparierte Liste der <see cref="CodeGenerationOptions"/>-Namen für den
    /// Hilfetext der <c>-g|genopts</c>-Option.</summary>
    static string GenerationOptionsString() {
        return string.Join(", ", Enum.GetNames(typeof(CodeGenerationOptions)));
    }

    /// <summary>Parst den Wert der <c>-g|genopts</c>-Option in <see cref="CodeGenerationOptions"/>; ein
    /// leerer Wert ergibt <see cref="CodeGenerationOptions.None"/>.</summary>
    /// <param name="value">Der Optionswert (ein oder mehrere <see cref="CodeGenerationOptions"/>-Namen).</param>
    /// <returns>Die geparste Bitmaske.</returns>
    static CodeGenerationOptions ParseGenerationOptions(string value) {
        if (value.IsNullOrEmpty()) {
            return CodeGenerationOptions.None;
        }

        return (CodeGenerationOptions)Enum.Parse(typeof(CodeGenerationOptions), value);
    }
}