using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyCopyright("Copyright ©  2018")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: AssemblyTitle(MyAssembly.ProductName)]
[assembly: AssemblyProduct(MyAssembly.ProductName)]
[assembly: AssemblyDescription(MyAssembly.ProductName)]
// AssemblyVersion = stabile Binding-Identität (Major.Minor.0.0); FileVersion trägt die volle
// Buildnummer, InformationalVersion zusätzlich Branch + Kurz-SHA. Werte stammen aus der
// git-abgeleiteten Berechnung (siehe _build\Version.targets), generiert nach MyAssembly.
[assembly: AssemblyVersion(MyAssembly.AssemblyVersion)]
[assembly: AssemblyFileVersion(MyAssembly.ProductVersion)]
[assembly: AssemblyInformationalVersion(MyAssembly.ProductVersionInformational)]

// IsExternalInit ist ab .NET 5 in der Runtime eingebaut. Auf netstandard2.0/net472 fehlt der Typ
// und wird hier als Polyfill für record/init bereitgestellt. Auf net10 (z.B. nav.exe nach Retarget)
// würde die eigene Definition mit dem eingebauten Typ kollidieren (CS0436) — daher ausklammern.
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices;

internal static class IsExternalInit {}
#endif