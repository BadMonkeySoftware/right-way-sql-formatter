#!/usr/bin/env node
/**
 * package-all.js
 *
 * Builds one platform-specific .vsix per supported platform into dist/.
 * The Marketplace serves each user only their platform's package, so
 * downloads carry a single SqlFormatter binary instead of all six.
 *
 * Usage:
 *   npm run package:all
 *
 * For each target this runs `vsce package --target <target>`; vsce invokes
 * the vscode:prepublish script, which reads RWSQL_TARGET (set here) so the
 * prepublish build publishes only that platform's binary into bin/.
 *
 * Publish everything afterwards with:
 *   npx vsce publish --packagePath dist/*.vsix
 */

const { execFileSync } = require('child_process');
const path = require('path');
const fs = require('fs');
const { TARGETS } = require('./targets');

const EXT_DIR = path.resolve(__dirname, '..');
const DIST_DIR = path.join(EXT_DIR, 'dist');
const VSCE = path.join(EXT_DIR, 'node_modules', '.bin', process.platform === 'win32' ? 'vsce.cmd' : 'vsce');

if (!fs.existsSync(VSCE)) {
    console.error('ERROR: vsce not found — run `npm install` first.');
    process.exit(1);
}

fs.mkdirSync(DIST_DIR, { recursive: true });

const results = [];
for (const target of TARGETS) {
    console.log(`\n=== Packaging ${target.vsce} ===`);
    execFileSync(VSCE, ['package', '--target', target.vsce, '--out', DIST_DIR + path.sep], {
        cwd: EXT_DIR,
        stdio: 'inherit',
        shell: process.platform === 'win32',
        env: { ...process.env, RWSQL_TARGET: target.vsce },
    });
    const version = JSON.parse(fs.readFileSync(path.join(EXT_DIR, 'package.json'), 'utf8')).version;
    const vsix = path.join(DIST_DIR, `right-way-sql-formatter-${target.vsce}-${version}.vsix`);
    if (!fs.existsSync(vsix)) {
        console.error(`ERROR: expected package not found: ${vsix}`);
        process.exit(1);
    }
    results.push({ target: target.vsce, vsix, mb: (fs.statSync(vsix).size / 1024 / 1024).toFixed(1) });
}

console.log('\n=== Packages built ===');
for (const r of results) console.log(`  ${r.target.padEnd(14)} ${r.mb.padStart(6)} MB  ${path.relative(EXT_DIR, r.vsix)}`);
console.log('\nPublish with:\n  npx vsce publish --packagePath dist/*.vsix');
