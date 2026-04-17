#!/usr/bin/env node
/**
 * build-cli.js
 *
 * Publishes a self-contained SqlFormatter binary into vscode-extension/bin/.
 * Run via: npm run build:cli
 *
 * What it does:
 *   1. Locates the .NET SDK (checks ~/.dotnet, then PATH)
 *   2. Runs `dotnet publish` on the CLI project with self-contained + single-file flags
 *   3. Makes the output binary executable
 *
 * No manual exports, no manual copies. Just: npm run build:cli
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
const CLI_PROJECT = path.join(REPO_ROOT, 'PoorMansTSqlFormatterCmdLine', 'PoorMansTSqlFormatterCmdLine.csproj');
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

console.log('Building self-contained SqlFormatter binary...');

const dotnet = findDotnet();
if (!dotnet) {
    console.error([
        'ERROR: .NET SDK not found.',
        'Install it with:',
        '  curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir ~/.dotnet',
    ].join('\n'));
    process.exit(1);
}

console.log(`  dotnet: ${dotnet}`);
console.log(`  project: ${CLI_PROJECT}`);
console.log(`  output:  ${BIN_DIR}`);
console.log(`  rid:     ${getRid()}`);

if (!fs.existsSync(CLI_PROJECT)) {
    console.error(`ERROR: CLI project not found at ${CLI_PROJECT}`);
    console.error('Make sure you are running this from inside the right-way-sql-formatter repo.');
    process.exit(1);
}

// Ensure output directory exists
fs.mkdirSync(BIN_DIR, { recursive: true });

// Run dotnet publish
try {
    execFileSync(dotnet, [
        'publish', CLI_PROJECT,
        '-c', 'Release',
        '-r', getRid(),
        '--self-contained', 'true',
        '-p:PublishSingleFile=true',
        '-o', BIN_DIR,
        '--nologo',
    ], {
        stdio: 'inherit',
        env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1' },
    });
} catch (err) {
    console.error('ERROR: dotnet publish failed.');
    process.exit(1);
}

// Ensure the binary is executable (no-op on Windows)
if (process.platform !== 'win32' && fs.existsSync(BINARY_PATH)) {
    fs.chmodSync(BINARY_PATH, 0o755);
}

if (fs.existsSync(BINARY_PATH)) {
    const size = (fs.statSync(BINARY_PATH).size / 1024 / 1024).toFixed(1);
    console.log(`\nDone. Binary: ${BINARY_PATH} (${size} MB)`);
} else {
    console.error(`ERROR: Expected binary not found at ${BINARY_PATH}`);
    process.exit(1);
}
