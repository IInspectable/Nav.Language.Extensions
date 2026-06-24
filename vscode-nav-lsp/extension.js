'use strict';

const path = require('path');
const fs = require('fs');
const cp = require('child_process');
const { workspace, window } = require('vscode');
const { LanguageClient, TransportKind, RevealOutputChannelOn } = require('vscode-languageclient/node');

let client;
let log;

function resolveServerDll() {

    const configured = workspace.getConfiguration('navLanguageServer').get('serverPath');
    if (configured && configured.trim().length > 0) {
        return configured;
    }

    // Standard: Build-Ausgabe im Repo. Die Extension liegt als Schwesterordner von Nav.Language.Server.
    return path.join(
        __dirname, '..',
        'Nav.Language.Server', 'bin', 'Debug', 'net10.0',
        'Pharmatechnik.Nav.Language.Server.dll');
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

    const serverDll = resolveServerDll();
    log.appendLine(`Server-DLL: ${serverDll}`);
    log.appendLine(`DLL existiert: ${fs.existsSync(serverDll)}`);
    checkDotnet();

    const serverOptions = {
        run:   { command: 'dotnet', args: [serverDll], transport: TransportKind.stdio },
        debug: { command: 'dotnet', args: [serverDll], transport: TransportKind.stdio }
    };

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
