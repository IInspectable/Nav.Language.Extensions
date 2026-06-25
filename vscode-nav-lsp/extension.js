'use strict';

const path = require('path');
const fs = require('fs');
const cp = require('child_process');
const { workspace, window, commands, Uri, Position, Range, Location } = require('vscode');
const { LanguageClient, TransportKind, RevealOutputChannelOn } = require('vscode-languageclient/node');

let client;
let log;

// Liefert das Start-Kommando für den Server als { command, args }.
// - .exe (z.B. der self-contained Publish nav.lsp.exe) wird direkt gestartet.
// - .dll wird über 'dotnet <dll>' gestartet (framework-dependent Debug-Build).
function resolveServer() {

    const configured = workspace.getConfiguration('navLanguageServer').get('serverPath');
    if (configured && configured.trim().length > 0) {
        const p = configured.trim();
        if (p.toLowerCase().endsWith('.dll')) {
            return { command: 'dotnet', args: [p], target: p };
        }
        return { command: p, args: [], target: p };
    }

    // Standard 0 (paketiert): in die Extension eingebetteter Server (server\nav.lsp.exe neben extension.js).
    // Greift im installierten VSIX, wo es KEINEN Schwesterordner deploy\lsp gibt — direkt starten.
    const bundledExe = path.join(__dirname, 'server', 'nav.lsp.exe');
    if (fs.existsSync(bundledExe)) {
        return { command: bundledExe, args: [], target: bundledExe };
    }

    // Die Extension liegt im Repo als Schwesterordner von Nav.Language.Lsp / deploy (F5-Dev).
    // Standard 1: self-contained Publish (deploy\lsp\nav.lsp.exe) — direkt starten, kein 'dotnet' nötig.
    const publishedExe = path.join(__dirname, '..', 'deploy', 'lsp', 'nav.lsp.exe');
    if (fs.existsSync(publishedExe)) {
        return { command: publishedExe, args: [], target: publishedExe };
    }

    // Standard 2: framework-dependent Debug-Build via 'dotnet'.
    const debugDll = path.join(
        __dirname, '..',
        'Nav.Language.Lsp', 'bin', 'Debug', 'net10.0', 'nav.lsp.dll');
    return { command: 'dotnet', args: [debugDll], target: debugDll };
}

function checkDotnet() {
    try {
        const out = cp.execSync('dotnet --version', { encoding: 'utf8' }).trim();
        log.appendLine(`dotnet gefunden: ${out}`);
        return true;
    } catch (err) {
        log.appendLine(`FEHLER: 'dotnet' nicht ausführbar (${err.message}). Ist die .NET-Runtime im PATH von VS Code?`);
        return false;
    }
}

function activate(context) {

    log = window.createOutputChannel('Nav Language Server');
    log.appendLine('Aktivierung der Nav-LSP-Extension …');

    const server = resolveServer();
    log.appendLine(`Server-Kommando: ${server.command} ${server.args.join(' ')}`.trim());
    log.appendLine(`Ziel existiert: ${fs.existsSync(server.target)}`);
    // 'dotnet' muss nur im PATH sein, wenn der Server framework-dependent (als .dll) gestartet wird.
    if (server.command === 'dotnet') {
        checkDotnet();
    }

    // Klick-Ziel der CodeLens „N Verweise". Der Server kann editor.action.showReferences nicht direkt
    // ansteuern, weil dessen Argumente echte vscode-Typen sein müssen, die der LanguageClient für freie
    // Command-Argumente nicht konvertiert. Daher wandelt dieser Command die JSON-Argumente (URI-String,
    // LSP-Position, LSP-Locations) in vscode.Uri/Position/Location um und reicht sie weiter.
    context.subscriptions.push(
        commands.registerCommand('nav.showReferences', (uriStr, position, locations) => {
            const uri = Uri.parse(uriStr);
            const pos = new Position(position.line, position.character);
            const locs = (locations || []).map(l => new Location(
                Uri.parse(typeof l.uri === 'string' ? l.uri : String(l.uri)),
                new Range(
                    l.range.start.line, l.range.start.character,
                    l.range.end.line, l.range.end.character)));
            return commands.executeCommand('editor.action.showReferences', uri, pos, locs);
        }));

    const launch = { command: server.command, args: server.args, transport: TransportKind.stdio };
    const serverOptions = { run: launch, debug: launch };

    const clientOptions = {
        documentSelector: [{ scheme: 'file', language: 'nav' }],
        outputChannel: log,
        // Das Panel NICHT automatisch in den Vordergrund holen: ein Fokuswechsel weg vom Editor
        // löscht in VS Code die Occurrence-Highlights (documentHighlight). Kanal bei Bedarf manuell öffnen.
        revealOutputChannelOn: RevealOutputChannelOn.Never,
        synchronize: {
            // Externe Änderungen an *.nav-Dateien (auch geschlossenen) an den Server melden
            // (workspace/didChangeWatchedFiles) — damit inkludierende Dateien neu diagnostiziert werden.
            // Zusätzlich .navignore beobachten: Ändern sich die Ignore-Regeln, lädt der Server sie neu und
            // publiziert alle Diagnostics erneut (neu ignoriert → löschen, nicht mehr ignoriert → anzeigen).
            fileEvents: [
                workspace.createFileSystemWatcher('**/*.nav'),
                workspace.createFileSystemWatcher('**/.navignore')
            ]
        }
    };

    client = new LanguageClient('navLanguageServer', 'Nav Language Server', serverOptions, clientOptions);

    client.start().then(
        () => log.appendLine('LanguageClient gestartet, Server verbunden.'),
        err => {
            log.appendLine(`FEHLER beim Start des LanguageClient: ${err && err.stack || err}`);
            window.showErrorMessage(`Nav Language Server konnte nicht gestartet werden: ${err}`);
        });
}

function deactivate() {
    return client ? client.stop() : undefined;
}

module.exports = { activate, deactivate };
