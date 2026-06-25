#:property ManagePackageVersionsCentrally=false
#:package StringTemplate4@4.0.8

// File-based .NET-Programm (dotnet run GenerateCodeGenFacts.cs -- <stg> <out> <namespace> <className>).
//
// Erzeugt aus einer StringTemplate-Group-Datei (.stg) eine C#-Datei mit den gerenderten Templates
// als public-const-Strings. Ersetzt den früheren CodeTaskFactory-Inline-Task aus
// Nav.Language\CustomBuild.targets (CodeTaskFactory existiert in .NET-Core-MSBuild nicht → MSB4801),
// damit die Engine auch mit `dotnet build` baut. Die Render-Logik ist 1:1 aus dem alten Task
// übernommen und nutzt dieselbe StringTemplate-Library wie die Laufzeit → byte-identische Ausgabe.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Antlr4.StringTemplate;

if (args.Length != 4) {
    Console.Error.WriteLine("Usage: GenerateCodeGenFacts <stgPath> <outPath> <namespace> <className>");
    return 1;
}

var stgPath    = args[0];
var outPath    = args[1];
var @namespace = args[2];
var className  = args[3];

// Wrapper-Template, das die einzelnen gerenderten Templates als const-Felder ausgibt.
// Verbatim-String (damit die ST-Escapes \< und \r\n als literale Backslash-Sequenzen erhalten
// bleiben); die echten Zeilenumbrüche werden anschließend auf CRLF normalisiert, damit die
// Ausgabe unabhängig von den Zeilenenden dieser Quelldatei stets CRLF ist.
const string templateTextRaw = @"
Begin(namespace, className, data) ::=<<// ReSharper disable once CheckNamespace
namespace <namespace> {
    public static partial class <className> {
        <data:writeProperty(); separator=""\r\n"">
    }
}
>>

writeProperty(kvp)  ::=<<
/// \<summary>
/// <kvp.Value>
/// \</summary>
public const string <kvp.Key> = ""<kvp.Value>"";
>>
";

var templateText = templateTextRaw.Replace("\r\n", "\n").Replace("\n", "\r\n");

var templateGroup = new TemplateGroupFile(Path.GetFullPath(stgPath));

var data = new Dictionary<string, string>();
foreach (var templateName in templateGroup.GetTemplateNames()) {

    var name = templateName.Substring(1, 1).ToUpperInvariant() + templateName.Substring(2);
    var st   = templateGroup.GetInstanceOf(name);

    data[name] = st.Render();
}

var group    = new TemplateGroupString(templateText);
var template = group.GetInstanceOf("Begin");
template.Add("className", className);
template.Add("namespace", @namespace);
template.Add("data",      data.ToList());

File.WriteAllText(outPath, template.Render(), Encoding.UTF8);

return 0;
