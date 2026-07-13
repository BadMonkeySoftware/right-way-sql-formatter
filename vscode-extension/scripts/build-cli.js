#!/usr/bin/env node
/**
 * build-cli.js
 *
 * Publishes self-contained SqlFormatter binaries for all major platforms into vscode-extension/bin/<rid>/.
 * Run via: npm run build:cli
 *
 * What it does:
 *   1. Locates the .NET SDK (checks ~/.dotnet, then PATH)
 *   2. Runs `dotnet publish` for each target RID (win-x64, win-arm64, osx-x64, osx-arm64, linux-x64, linux-arm64)
 *   3. Outputs each binary to bin/<rid>/SqlFormatter[.exe]
 *   4. Makes non-Windows binaries executable
 *
 * No manual exports, no manual copies. Just: npm run build:cli
 *
 * If you add new platforms, update the targets array below and the extension's resolveExecutablePath().
 */

const { execFileSync } = require('child_process');
const path = require('path');
const fs = require('fs');
const os = require('os');

// ---------------------------------------------------------------------------
// Paths
// ---------------------------------------------------------------------------

// This script lives in vscode-extension/scripts/ — repo root is two levels up
const REPO_ROOT = path.resolve(__dirname, '..', '..');
const CLI_PROJECT = path.join(REPO_ROOT, 'RightWaySqlFormatter.CmdLine', 'RightWaySqlFormatter.CmdLine.csproj');
const BIN_DIR = path.join(__dirname, '..', 'bin');
const BINARY_NAME = process.platform === 'win32' ? 'SqlFormatter.exe' : 'SqlFormatter';
const BINARY_PATH = path.join(BIN_DIR, BINARY_NAME);

// Runtime identifier — matches the machine we're building on
function getRid() {
    const plat = process.platform;
    const arch = process.arch;
    if (plat === 'darwin') return arch === 'arm64' ? 'osx-arm64' : 'osx-x64';
    if (plat === 'win32')  return arch === 'arm64' ? 'win-arm64' : 'win-x64';
    return arch === 'arm64' ? 'linux-arm64' : 'linux-x64';
}

// ---------------------------------------------------------------------------
// Find dotnet
// ---------------------------------------------------------------------------

function findDotnet() {
    // 1. ~/.dotnet/dotnet (installed via dotnet-install.sh, no sudo)
    const home = os.homedir();
    const homeDotnet = path.join(home, '.dotnet', 'dotnet');
    if (fs.existsSync(homeDotnet)) return homeDotnet;

    // 2. /usr/local/share/dotnet/dotnet (macOS pkg installer)
    const macPkg = '/usr/local/share/dotnet/dotnet';
    if (fs.existsSync(macPkg)) return macPkg;

    // 3. PATH
    try {
        const which = process.platform === 'win32' ? 'where' : 'which';
        const result = execFileSync(which, ['dotnet'], { encoding: 'utf8' }).trim().split('\n')[0].trim();
        if (result && fs.existsSync(result)) return result;
    } catch { /* not on PATH */ }

    return null;
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------


console.log('Building self-contained SqlFormatter binaries for all platforms...');

const dotnet = findDotnet();
if (!dotnet) {
    console.error([
        'ERROR: .NET SDK not found.',
        'Install it with:',
        '  curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir ~/.dotnet',
    ].join('\n'));
    process.exit(1);
}

if (!fs.existsSync(CLI_PROJECT)) {
    console.error(`ERROR: CLI project not found at ${CLI_PROJECT}`);
    console.error('Make sure you are running this from inside the right-way-sql-formatter repo.');
    process.exit(1);
}

// Target RIDs for all major platforms
const targets = [
    { rid: 'win-x64',    exe: 'SqlFormatter.exe' },
    { rid: 'win-arm64',  exe: 'SqlFormatter.exe' },
    { rid: 'osx-x64',    exe: 'SqlFormatter' },
    { rid: 'osx-arm64',  exe: 'SqlFormatter' },
    { rid: 'linux-x64',  exe: 'SqlFormatter' },
    { rid: 'linux-arm64',exe: 'SqlFormatter' },
];

for (const target of targets) {
    const outDir = path.join(BIN_DIR, target.rid);
    fs.mkdirSync(outDir, { recursive: true });
    console.log(`\nPublishing for ${target.rid}...`);
    try {
        execFileSync(dotnet, [
            'publish', CLI_PROJECT,
            '-c', 'Release',
            '-r', target.rid,
            '--self-contained', 'true',
            '-p:PublishSingleFile=true',
            '-o', outDir,
            '--nologo',
        ], {
            stdio: 'inherit',
            env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1' },
        });
        // Make executable on non-Windows
        const binPath = path.join(outDir, target.exe);
        if (!target.exe.endsWith('.exe') && fs.existsSync(binPath)) {
            fs.chmodSync(binPath, 0o755);
        }
        if (fs.existsSync(binPath)) {
            const size = (fs.statSync(binPath).size / 1024 / 1024).toFixed(1);
            console.log(`  Done: ${binPath} (${size} MB)`);
        } else {
            console.error(`  ERROR: Expected binary not found at ${binPath}`);
            process.exit(1);
        }
    } catch (err) {
        console.error(`  ERROR: dotnet publish failed for ${target.rid}.`);
        process.exit(1);
    }
}

console.log('\nAll platform binaries built successfully.');
