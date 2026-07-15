# Plan: dual SSMS extensions (SSMS 18 and SSMS 21/22)

Decision (Jeremy, 2026-07-15): ship TWO SSMS extensions from this repo —
one for the SSMS 18 generation (huge remaining install base; the original
PoorMans plugin's home turf) and one for SSMS 21/22 (VS 2022 shell).
Execution happens on the Windows VM with Claude Code, per
[windows-ssms-dev.md](windows-ssms-dev.md).

## Why two, not one

- SSMS 18 = VS 2017-era shell, **32-bit** process, VSSDK 15.x interop.
- SSMS 21/22 = VS 2022 shell, **64-bit** (ARM64 hosts run an ARM64 CLR),
  VSSDK 17.x interop, no official extension support (see windows-ssms-dev.md).
- A single VSIX cannot target both shells; the interop assemblies and
  manifest InstallationTargets are incompatible.

## Target architecture

```
RightWaySqlFormatter        (core, net472 + net10.0)   <- all formatting logic
        |
RightWaySqlFormatter.PluginShared (net472)             <- generic plugin glue
        |
RightWaySqlFormatter.SSMSLib      (net472)             <- shell-AGNOSTIC SSMS logic:
        |                                                  EnvDTE text manipulation,
        |                                                  command handlers, options
   +----+---------------------------+
   |                                |
RightWaySqlFormatter.SSMS18    RightWaySqlFormatter.SSMS22
(thin package project)         (thin package project)
```

The two package projects should contain ONLY:

- csproj with the shell-specific VSSDK references
  (SSMS18: Microsoft.VisualStudio.Shell.15.0-era packages;
   SSMS22: Microsoft.VSSDK.BuildTools / Microsoft.VisualStudio.Sdk 17.x)
- `source.extension.vsixmanifest` with the matching InstallationTarget
- the Package class (AsyncPackage), .vsct command table, pkgdef wiring
- icon/branding

Everything else lives in SSMSLib. If a piece of code needs a
`Microsoft.VisualStudio.Shell.*` type, it does NOT belong in SSMSLib —
abstract it behind an interface implemented in each package project.
EnvDTE types are fine in SSMSLib (same COM interop for both shells).

## Bitness

- Both packages: **AnyCPU, Prefer32Bit off.** AnyCPU loads in SSMS 18's
  32-bit process, SSMS 22's x64 process, and ARM64 CLR hosts alike.
  Never PlatformTarget=x64 (breaks ARM64) or x86 (breaks SSMS 22).

## Migration steps (on the VM, in order)

1. Rename the existing `RightWaySqlFormatter.SSMSPackage` ->
   `RightWaySqlFormatter.SSMS18` (it is already the SSMS 18-era package).
   Verify it still builds with msbuild and deploys to SSMS 18 if present;
   otherwise verify against upstream's known-working configuration.
2. Extract any Shell.*-dependent code that crept into SSMSLib into the
   SSMS18 project (SSMSLib must compile against no VSSDK, only EnvDTE).
   Verified 2026-07-15: SSMSLib currently has NO Microsoft.VisualStudio.Shell
   references (GenericVSHelper is EnvDTE-only) - this step is likely a no-op;
   just keep it true. The existing SSMSPackage references Shell.15.0,
   confirming it is the SSMS 18-era package.
3. Create `RightWaySqlFormatter.SSMS22`: new VSIX project on VSSDK 17.x,
   AsyncPackage, same .vsct commands, referencing SSMSLib. Manifest
   InstallationTarget must match the SSMS 22 shell identity (discover it
   from an installed extension-ish component on the VM or the SSMS
   devenv.isolation config; expect trial and error - ActivityLog.xml is
   the oracle).
4. Deploy per windows-ssms-dev.md (Extensions folder + `ssms.exe /setup`),
   SSMS 18 variant goes to
   `C:\Program Files (x86)\...\SSMS 18\Common7\IDE\Extensions\`,
   SSMS 22 to `C:\Program Files\...\SSMS 22\Release\Common7\IDE\Extensions\`.
5. Add both projects to `RightWaySqlFormatter.slnx` (Windows-only solution);
   NoSSMS.slnx stays untouched.
6. Update README project table + windows-ssms-dev.md with what actually
   worked (paths, InstallationTarget values, gotchas).

## Constraints and cautions

- Formatting logic changes stay on the normal macOS workflow; the VM work
  is shell integration only.
- SSMS 18 is officially extension-tolerant but unsupported; SSMS 21/22 is
  explicitly unsupported (no Extension Manager). Both are personal-use
  deployments until/unless Microsoft ships real SSMS extensibility.
- Test-suite rules are unchanged: `dotnet test RightWaySqlFormatter.NoSSMS.slnx`
  must stay green; the SSMS projects have no automated tests (manual smoke
  test in each SSMS: format document, format selection, options round-trip).
- Distribution (installer/zip per target) is out of scope for the first
  pass; get both loading and formatting first.
