#region Using Directives

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using ModelContextProtocol.Server;

using Pharmatechnik.Nav.Language.CodeGen;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>nav_preview_codegen</c>: liefert den C#-Code, der aus einer <c>.nav</c>-Datei generiert
/// würde — <b>ohne Plattenschreiben und ohne Build</b>. Engine-Kern ist dieselbe Codegen-Pipeline, die
/// <c>nav.exe</c> und der MSBuild-Task nutzen (<see cref="ICodeGeneratorProvider"/> →
/// <see cref="CodeGeneratorV1"/>/<c>CodeGeneratorV2</c>, je nach Sprachversion der Datei). Statt für die
/// Frage „welcher C# entsteht" erst <c>nav.exe</c> laufen zu lassen und die <c>*.generated.cs</c> zu
/// suchen, generiert das Tool in-memory gegen das frisch von Platte gelesene semantische Modell.
/// </summary>
[McpServerToolType]
public static class NavPreviewCodegenTool {

    /// <summary>
    /// Obergrenze für den zurückgegebenen Gesamtinhalt (Zeichen). Wird sie überschritten, entfällt der
    /// Inhalt aller Artefakte und nur das Manifest bleibt — sicher unter dem MCP-Tool-Result-Limit
    /// (~25k Tokens). Der Agent grenzt dann mit <c>task</c> ein oder holt gezielt nach.
    /// </summary>
    const int MaxContentChars = 60_000;

    [McpServerTool(Name = "nav_preview_codegen")]
    [Description("Previews the C# code generated from a Nav (.nav) file WITHOUT writing to disk and WITHOUT " +
                 "a build — the same code generation pipeline nav.exe and the MSBuild task use. For each task " +
                 "definition it returns the generated artifacts: the abstract base class ('base') carrying the " +
                 "abstract logic methods with their transitively reachable DI parameters, the interfaces "      +
                 "('iwfs', 'ibegin' — the Begin overloads), and optionally the user stub. Use this to learn "  +
                 "the exact generated method names, Begin overloads and DI parameters instead of running "      +
                 "nav.exe and reading *.generated.cs. Code generation requires an error-free file — if the "    +
                 "file has errors they are returned in 'diagnostics' and no code is generated (fix them first, " +
                 "see nav_validate/nav_diagnostics).")]
    public static NavPreviewCodegenResult PreviewCodegen(
        NavMcpWorkspace workspace,
        [Description("Absolute path to the .nav file.")]
        string path,
        [Description("Optional task name — preview only this task definition's artifacts. Omit to preview " +
                     "every task in the file.")]
        string? task = null,
        [Description("Include the user-owned scaffolds (the one-shot {Task}WFS class stub and any TO stubs). " +
                     "Default false: these are near-empty, never-overwritten stubs; the generated interfaces " +
                     "and the abstract base class carry the signatures an agent needs.")]
        bool includeUserFiles = false,
        [Description("Include the generated C# source of each artifact ('content'). Set false to get only the " +
                     "manifest (roles, file names, line/char counts). Default true.")]
        bool includeContent = true,
        [Description("Emit '#nullable enable' in the generated code. Default false (matches the default build).")]
        bool nullableContext = false,
        [Description("Optional project root directory. Only affects the namespaces of the generated code " +
                     "(derived relative to this root); leave empty if you only care about the method and " +
                     "parameter signatures.")]
        string? projectRoot = null) {

        var unit = workspace.GetFreshUnit(path, out var normalizedPath);
        if (unit == null) {
            return NavPreviewCodegenResult.NotFound(path);
        }

        var result = new NavPreviewCodegenResult {
            Path            = path,
            LanguageVersion = unit.LanguageVersion.Value
        };

        // Der Codegen läuft nur auf einem fehlerfreien Modell — andernfalls würde der Generator werfen
        // (ArgumentException auf Syntax-/Semantik-/Include-Fehler). Vorab prüfen und die Fehler strukturiert
        // zurückgeben (wie nav_validate), statt eine Protokoll-Exception zu riskieren.
        var documentDiagnostics = DiagnosticsComputer.FromUnit(unit, normalizedPath);
        var hasDocErrors        = documentDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        var hasIncludeErrors    = unit.Includes.Any(include => include.Diagnostics.HasErrors());

        if (hasDocErrors || hasIncludeErrors) {

            result.Diagnostics = documentDiagnostics
                                .Where(d => d.Severity == DiagnosticSeverity.Error)
                                .Select(d => NavDiagnosticDto.From(d, normalizedPath))
                                .ToList();

            result.Error = hasIncludeErrors
                ? "Codegen nicht möglich: die Datei oder eine per taskref inkludierte Datei enthält Fehler. Zuerst beheben (siehe nav_diagnostics)."
                : "Codegen nicht möglich: die Datei enthält Fehler. Zuerst beheben (siehe 'diagnostics').";

            return result;
        }

        var options = GenerationOptions.Default with {
            NullableContext      = nullableContext,
            ProjectRootDirectory = projectRoot ?? String.Empty
        };

        using var generator      = CodeGeneratorProvider.Default.Create(options, PathProviderFactory.Default);
        var codeGenerationResults = generator.Generate(unit);

        var selected = codeGenerationResults.AsEnumerable();
        if (!String.IsNullOrEmpty(task)) {

            selected = selected.Where(r => String.Equals(r.TaskDefinition.Name, task, StringComparison.Ordinal)).ToList();

            if (!selected.Any()) {
                result.Error = $"Task '{task}' ist in dieser Datei nicht definiert.";
                return result;
            }
        }

        foreach (var codeGenerationResult in selected) {

            var roleByFile = BuildRoleMap(codeGenerationResult.TaskDefinition, options);

            var taskDto = new NavPreviewTaskDto { Task = codeGenerationResult.TaskDefinition.Name };

            foreach (var spec in codeGenerationResult.Specs) {

                var role = ClassifyRole(spec.FilePath, roleByFile);

                // Benutzer-Eigentum (der einmalige Klassen-Stub und TO-Stubs) ist standardmäßig
                // uninteressant — die Signaturen stehen in Basisklasse + Interfaces.
                if (!includeUserFiles && (role == RoleUser || role == RoleTo)) {
                    continue;
                }

                taskDto.Artifacts.Add(new NavPreviewArtifactDto {
                    Role            = role,
                    FileName        = System.IO.Path.GetFileName(spec.FilePath),
                    LineCount       = CountLines(spec.Content),
                    CharCount       = spec.Content.Length,
                    OverwritePolicy = spec.OverwritePolicy.ToString(),
                    Content         = includeContent ? spec.Content : null
                });
            }

            if (taskDto.Artifacts.Count > 0) {
                result.Tasks.Add(taskDto);
            }
        }

        // Token-Budget-Wächter: sprengt der Gesamtinhalt die Obergrenze, nur das Manifest zurückgeben.
        if (includeContent) {

            var totalChars = result.Tasks.Sum(t => t.Artifacts.Sum(a => a.CharCount));

            if (totalChars > MaxContentChars) {
                result.ContentOmitted = true;
                foreach (var artifact in result.Tasks.SelectMany(t => t.Artifacts)) {
                    artifact.Content = null;
                }
            }
        }

        return result;
    }

    const string RoleIWfs   = "iwfs";
    const string RoleIBegin  = "ibegin";
    const string RoleBase   = "base";
    const string RoleUser   = "user";
    const string RoleTo     = "to";

    /// <summary>
    /// Bildet die kanonischen Ausgabe-Dateinamen einer Task-Definition auf ihre Rolle ab — autoritativ über
    /// denselben <see cref="IPathProvider"/>, den der Codegen benutzt. So wird jedes Artefakt eindeutig
    /// seiner Rolle zugeordnet, ohne den Dateinamen zu raten. Fehlt die <c>FileInfo</c> (kann bei direkt
    /// konstruierten Units passieren), bleibt die Map leer und <see cref="ClassifyRole"/> fällt auf die
    /// Suffix-Heuristik zurück.
    /// </summary>
    static Dictionary<string, string> BuildRoleMap(ITaskDefinitionSymbol taskDefinition, GenerationOptions options) {

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try {
            var pathProvider = PathProviderFactory.Default.CreatePathProvider(taskDefinition, options);
            Add(pathProvider.IWfsFileName,      RoleIWfs);
            Add(pathProvider.IBeginWfsFileName, RoleIBegin);
            Add(pathProvider.WfsBaseFileName,   RoleBase);
            Add(pathProvider.WfsFileName,       RoleUser);
        } catch (ArgumentException) {
            // Kein FileInfo verfügbar — Suffix-Heuristik übernimmt.
        }

        return map;

        void Add(string file, string role) {
            var key = PathHelper.NormalizePath(file);
            if (!String.IsNullOrEmpty(key)) {
                map[key!] = role;
            }
        }
    }

    /// <summary>
    /// Ordnet einen Artefakt-Pfad seiner Rolle zu: zunächst autoritativ über die
    /// <see cref="BuildRoleMap"/>, ersatzweise über das Dateinamen-Suffix (TO-Stubs sind über den
    /// PathProvider nicht vorab bekannt und landen stets hier).
    /// </summary>
    static string ClassifyRole(string filePath, Dictionary<string, string> roleByFile) {

        var normalized = PathHelper.NormalizePath(filePath);
        if (!String.IsNullOrEmpty(normalized) && roleByFile.TryGetValue(normalized!, out var role)) {
            return role;
        }

        // Fallback: die generierten Interfaces/Basisklasse tragen ".generated."; der Benutzer-Stub nicht.
        var fileName = System.IO.Path.GetFileName(filePath);
        var isGenerated = fileName.IndexOf(".generated.", StringComparison.OrdinalIgnoreCase) >= 0;

        if (!isGenerated) {
            return RoleUser;
        }

        if (fileName.StartsWith("IBegin", StringComparison.OrdinalIgnoreCase)) {
            return RoleIBegin;
        }

        if (fileName.StartsWith("I", StringComparison.Ordinal)) {
            return RoleIWfs;
        }

        if (fileName.IndexOf("Base.", StringComparison.OrdinalIgnoreCase) >= 0) {
            return RoleBase;
        }

        return RoleTo;
    }

    /// <summary>Zählt die Zeilen eines Textes (leerer Text = 0, sonst Anzahl der Zeilenumbrüche + 1).</summary>
    static int CountLines(string content) {

        if (content.Length == 0) {
            return 0;
        }

        var lines = 1;
        foreach (var c in content) {
            if (c == '\n') {
                lines++;
            }
        }

        return lines;
    }

}
