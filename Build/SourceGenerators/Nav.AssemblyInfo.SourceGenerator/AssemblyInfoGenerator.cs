using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using Pharmatechnik.Nav.Language.CodeAnalysis.Shared;

namespace Pharmatechnik.Nav.Language.AssemblyInfo.SourceGenerator;

/// <summary>
/// Inkrementeller Quellgenerator, der die <c>static partial class MyAssembly</c> mit den
/// Versions- und Produktkonstanten erzeugt. Die Werte stammen aus den MSBuild-Properties
/// <c>ProductVersion</c>, <c>AssemblyVersion</c>, <c>ProductVersionInformational</c> und
/// <c>ProductName</c> (git-abgeleitet in <c>_build/Version.targets</c>, über
/// <c>CompilerVisibleProperty</c> als <c>build_property.*</c> sichtbar gemacht).
///
/// Löst die frühere, ins Projektverzeichnis geschriebene <c>ThisAssembly.generated.cs</c> ab: Der
/// Generator-Output ist pro Compilation und wird nie als geteilte Datei abgelegt — die Falle, dass
/// ein VS-Design-Time-Build (mit leerlaufendem git → <c>0.0.0</c>) den korrekten Wert eines echten
/// Builds überschreibt, entfällt damit strukturell.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AssemblyInfoGenerator: IIncrementalGenerator {

    public void Initialize(IncrementalGeneratorInitializationContext context) {

        var values = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) => {
            var options = provider.GlobalOptions;
            return new AssemblyInfoValues(
                ProductVersion: Read(options, "build_property.ProductVersion", "0.0.0"),
                AssemblyVersion: Read(options, "build_property.AssemblyVersion", "0.0.0.0"),
                ProductVersionInformational: Read(options, "build_property.ProductVersionInformational", "0.0.0"),
                ProductName: Read(options, "build_property.ProductName", ""));
        });

        context.RegisterSourceOutput(values, static (spc, v) => Emit(spc, v));
    }

    static string Read(AnalyzerConfigOptions options, string key, string fallback) {
        return options.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
            ? value
            : fallback;
    }

    static void Emit(SourceProductionContext spc, AssemblyInfoValues v) {

        var sb = new SourceBuilder();
        sb.AppendHeader();
        sb.Block("static partial class MyAssembly", cls => {
            cls.AppendLine($"public const string ProductVersion = {Literal(v.ProductVersion)};");
            cls.AppendLine($"public const string AssemblyVersion = {Literal(v.AssemblyVersion)};");
            cls.AppendLine($"public const string ProductVersionInformational = {Literal(v.ProductVersionInformational)};");
            cls.AppendLine($"public const string ProductName = {Literal(v.ProductName)};");
        });

        spc.AddSource("MyAssembly.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    static string Literal(string value) {
        return SymbolDisplay.FormatLiteral(value, quote: true);
    }

}

/// <summary>Die vier je Assembly emittierten Konstanten-Werte (Wertgleichheit steuert das inkrementelle Caching).</summary>
sealed record AssemblyInfoValues(
    string ProductVersion,
    string AssemblyVersion,
    string ProductVersionInformational,
    string ProductName);
