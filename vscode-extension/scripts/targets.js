/**
 * Shared platform table: VS Code Marketplace target <-> .NET runtime identifier.
 * Used by build-cli.js (publish) and package-all.js (per-platform .vsix).
 * If you add a platform, also update the extension's resolveExecutablePath().
 */
const TARGETS = [
    { vsce: 'win32-x64',    rid: 'win-x64',     exe: 'SqlFormatter.exe' },
    { vsce: 'win32-arm64',  rid: 'win-arm64',   exe: 'SqlFormatter.exe' },
    { vsce: 'darwin-x64',   rid: 'osx-x64',     exe: 'SqlFormatter' },
    { vsce: 'darwin-arm64', rid: 'osx-arm64',   exe: 'SqlFormatter' },
    { vsce: 'linux-x64',    rid: 'linux-x64',   exe: 'SqlFormatter' },
    { vsce: 'linux-arm64',  rid: 'linux-arm64', exe: 'SqlFormatter' },
];

function hostRid() {
    const plat = process.platform;
    const arch = process.arch;
    if (plat === 'darwin') return arch === 'arm64' ? 'osx-arm64' : 'osx-x64';
    if (plat === 'win32')  return arch === 'arm64' ? 'win-arm64' : 'win-x64';
    return arch === 'arm64' ? 'linux-arm64' : 'linux-x64';
}

module.exports = { TARGETS, hostRid };
