#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Generator;

#endregion

namespace Pharmatechnik.Nav.Language.Logging; 

public class ConsoleLogger : ILogger {

    public ConsoleLogger(bool fullPaths, bool noWarnings = false, bool verbose = false, string verbosePrefix = null) {
        FullPaths     = fullPaths;
        NoWarnings    = noWarnings;
        Verbose       = verbose;
        VerbosePrefix = verbosePrefix ?? "Verbose:";
    }

    public bool   FullPaths     { get; }
    public bool   Verbose       { get; }
    public bool   NoWarnings    { get;}
    public string VerbosePrefix { get; }

    public void LogVerbose(string message) {
        if (!Verbose) {
            return;
        }
        WriteVerbose(message);
    }
        
    public void LogInfo(string message) {
        WriteInfo(message);
    }

    public void LogError(string message) {
        WriteError(message);
    }

    public void LogError(Diagnostic diag) {
        WriteError(FormatDiagnostic(diag));
    }

    public void LogWarning(Diagnostic diag) {
        if (NoWarnings) {
            return;
        }
        WriteWarning(FormatDiagnostic(diag));
    }

    protected virtual void WriteVerbose(string message) {
        WriteLine($"{VerbosePrefix}{message}", ConsoleColor.DarkGray);
    }

    protected virtual void WriteInfo(string message) {
        WriteLine($"{message}", Console.ForegroundColor);
    }

    protected virtual void WriteError(string message) {
        WriteLine(message, ConsoleColor.Red);
    }

    protected virtual void WriteWarning(string message) {
        WriteLine(message, ConsoleColor.Yellow);
    }

    void WriteLine(string message, ConsoleColor foregroundColor) {
        var oldBackground = Console.ForegroundColor;
        try {
            Console.ForegroundColor = foregroundColor;
            Console.WriteLine(message);
        } finally {
            Console.ForegroundColor = oldBackground;
        }
    }

    protected virtual string FormatDiagnostic(Diagnostic diag) {
        var formatter = new DiagnosticFormatter(
            displayEndLocations: FullPaths,
            workingDirectory   : FullPaths ? null : Environment.CurrentDirectory);

        return formatter.Format(diag);
    } 
}