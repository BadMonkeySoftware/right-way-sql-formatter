#!/usr/bin/env node
/**
 * build-cli.js
 *
 * Publishes self-contained, trimmed SqlFormatter binaries into
 * vscode-extension/bin/<rid>/SqlFormatter[.exe].
 *
 * Usage:
 *   node scripts/build-cli.js                  # all six platforms (default)
 *   node scripts/build-cli.js --target host    # just this machine (fast dev loop)
 *   node scripts/build-cli.js --target darwin-arm64   # one platform (VS Code target or .NET RID)
 *
 * The target may also be set via the RWSQL_TARGET env var (used by package-all.js).
 * Set RWSQL_NO_TRIM=1 to disable IL trimming (debugging escape hatch).
 *
 * bin/ is cleaned before every run so a single-target build never packages
 * stale binaries from other platforms.
 *
 * If you add new platforms, update TARGETS below, package-all.js, and the
 * extension's resolveExecutablePath().
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

const { TARGETS, hostRid } = require('./targets');

function resolveTargets(spec) {
    if (!spec || spec === 'all') return TARGETS;
    if (spec === 'host') spec = hostRid();
    const match = TARGETS.find(t => t.vsce === spec || t.rid === spec);
    if (!match) {
        console.error(`ERROR: unknown target '${spec}'.`);
        console.error(`Valid targets: all, host, ${TARGETS.map(t => t.vsce).join(', ')} (or the equivalent .NET RIDs)`);
        process.exit(1);
    }
    return [match];
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

const argIdx = process.argv.indexOf('--target');
const targetSpec = argIdx >= 0 ? process.argv[argIdx + 1] : (process.env.RWSQL_TARGET || 'all');
const targets = resolveTargets(targetSpec);
const noTrim = process.env.RWSQL_NO_TRIM === '1';

console.log(`Building self-contained SqlFormatter binaries (${targets.map(t => t.rid).join(', ')})${noTrim ? ' [trimming disabled]' : ''}...`);

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

// Clean bin/ so single-target packages never contain stale other-platform binaries
fs.rmSync(BIN_DIR, { recursive: true, force: true });
fs.mkdirSync(BIN_DIR, { recursive: true });

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
            // Trimming + single-file compression come from the CmdLine csproj
            // (PublishTrimmed / EnableCompressionInSingleFile). Do NOT pass
            // -p:PublishTrimmed=true globally here: it flows into the core
            // library's net472 (SSMS) restore and fails with NETSDK1124.
            // A global =false is safe, so the escape hatch works as an override.
            ...(noTrim ? ['-p:PublishTrimmed=false'] : []),
            // No debug symbols in shipping binaries.
            '-p:DebugType=none',
            '-p:DebugSymbols=false',
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

console.log('\nAll requested platform binaries built successfully.');
