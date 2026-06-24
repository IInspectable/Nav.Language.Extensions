'use strict';

const path = require('path');
const fs = require('fs');
const cp = require('child_process');
const { workspace, window } = require('vscode');
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

    // Die Extension liegt als Schwesterordner von Nav.Language.Server / deploy.
    // Standard 1: self-contained Publish (deploy\lsp\nav.lsp.exe) — direkt starten, kein 'dotnet' nötig.
    const publishedExe = path.join(__dirname, '..', 'deploy', 'lsp', 'nav.lsp.exe');
    if (fs.existsSync(publishedExe)) {
        return { command: publishedExe, args: [], target: publishedExe };
    }

    // Standard 2: framework-dependent Debug-Build via 'dotnet'.
    const debugDll = path.join(
        __dirname, '..',
        'Nav.Language.Server', 'bin', 'Debug', 'net10.0', 'nav.lsp.dll');
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

    const launch = { command: server.command, args: server.args, transport: TransportKind.stdio };
    const serverOptions = { run: launch, debug: launch };

    const clientOptions = {
        documentSelector: [{ scheme: 'file', language: 'nav' }],
        outputChannel: log,
        // Das Panel NICHT automatisch in den Vordergrund holen: ein Fokuswechsel weg vom Editor
        // löscht in VS Code die Occurrence-Highlights (documentHighlight). Kanal bei Bedarf manuell öffnen.
        revealOutputChannelOn: RevealOutputChannelOn.Never
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
