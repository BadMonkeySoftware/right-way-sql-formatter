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

1. DONE. macOS 2026-07-15 (rename half): folder + csproj renamed to
   `RightWaySqlFormatter.SSMS18`, solution/doc references updated.
   VM 2026-07-15 (build + AssemblyName decision): built green with
   `msbuild` under VS 18 Community (0 errors; 14 pre-existing warnings).
   Decision (Jeremy): align the AssemblyName of ALL THREE plugin
   assemblies now, not piecemeal — `PoorMansTSqlFormatterSSMSPackage` ->
   `RightWaySqlFormatter.SSMS18`, `PoorMansTSqlFormatterSSMSLib` ->
   `RightWaySqlFormatter.SSMSLib`, `PoorMansTSqlFormatterPluginShared` ->
   `RightWaySqlFormatter.PluginShared`.
   - Convention: changed `AssemblyName` ONLY; C# namespaces stay
     `PoorMansTSqlFormatter*` (matches the core project, which is
     `AssemblyName=RightWaySqlFormatter` / `RootNamespace=PoorMansTSqlFormatter`).
     Keeping the namespaces protects the hard-coded resource base name
     (`GenericVSHelper` `ResourceManager("PoorMansTSqlFormatterSSMSLib.GeneralLanguageContent")`)
     and the settings section (`PoorMansTSqlFormatterSSMSLib.Properties.Settings`
     in app.config) — both are namespace-based, not assembly-name-based.
   - Verified against a real build (the whole reason this was deferred to
     the VM): pkgdef regenerates `CodeBase=$PackageFolder$\RightWaySqlFormatter.SSMS18.dll`
     and keeps `Class=PoorMansTSqlFormatterSSMSPackage.FormatterPackage`
     (namespace unchanged, type still present) — both resolve. Package
     GUID `{247609b1-...}` (the actual registration identity) unchanged.
     VSIX payload is a consistent matched set (renamed DLLs + satellites
     `RightWaySqlFormatter.SSMSLib.resources.dll` + `.dll.config`, `<Asset
     Path="RightWaySqlFormatter.SSMS18.pkgdef">`); no stale `PoorMans*`
     output files. `dotnet test RightWaySqlFormatter.NoSSMS.slnx` still
     0 failed / 579 passed / 10 skipped.
   - Deferred cosmetic leftovers (no functional ripple, not done here):
     `AssemblyTitle`/`AssemblyProduct` strings in both AssemblyInfo.cs,
     the `ProductName` in `VSPackage.resx` (Help>About text), and the two
     stale `<ProjectReference><Name>` hints in the SSMS18 csproj.
2. DONE 2026-07-15 (confirmed no-op, both axes): SSMSLib references only
   the EnvDTE/EnvDTE80 interop packages (17.6.36 - COM interfaces, stable
   across both shells) and PluginShared references nothing VS-related;
   neither project's source uses any Microsoft.VisualStudio.* type.
   Standing rule remains: if code needs a Shell.* type it belongs in a
   package project, not SSMSLib.
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
   SSMS 18: DONE 2026-07-15 — deploys, loads, and formats (whole document +
   selection); options dialog and localized (es/fr) resources OK. Deploy the
   extracted VSIX payload into `...\SSMS 18\Common7\IDE\Extensions\RightWaySqlFormatter\`,
   then run `Ssms.exe /setup` **elevated**. Registration is by package GUID
   `{247609b1-...}`. ActivityLog for SSMS 18 is at
   `%AppData%\Microsoft\AppEnv\15.0\ActivityLog.xml` (VS15 isolation root),
   not the `SQL Server Management Studio\18.0_*` path. Two PRE-EXISTING
   incompatibilities surfaced on the first real load and were fixed (both
   shell-integration only, no formatter change):
   - **Strong-name mismatch.** The signed package + SSMSLib referenced the
     unsigned core + PluginShared → `SetSite failed ... A strongly-named
     assembly is required` (HRESULT 0x80131044). Fix: un-signed all four
     plugin assemblies (removed `SignAssembly`/`Key.snk`) for a consistent
     weak-named chain; unsigned VSIX-style extensions load fine.
   - **EnvDTE interop version.** The modern EnvDTE 17.6.36 packages bind to
     `Microsoft.VisualStudio.Interop 17.0.0.0` (a VS2022 assembly SSMS 18
     lacks), so the command handlers/`QueryClose` threw at JIT. Fix: use the
     classic embeddable EnvDTE/EnvDTE80 **v8.0.0.0** PIAs (vendored under
     `lib/interop/`) with `EmbedInteropTypes=true`. SSMSLib now embeds them
     with ZERO external EnvDTE/Interop reference (needed a `Microsoft.CSharp`
     ref for the late-bound `object`->`TextSelection` COM casts, CS0656).
     The package project still resolves EnvDTE 8.0.0.0 from the shell (the
     VS SDK meta-package's copy wins over the vendored ref) — acceptable, as
     it is shell-specific and both SSMS 18 and 22 provide EnvDTE 8.0.0.0.
3. Create `RightWaySqlFormatter.SSMS22` following the WORKING TEMPLATE:
   ErikEJ's SqlAnalyzerSsms (https://github.com/ErikEJ/SqlServer.Rules/tree/master/tools/SqlAnalyzerSsms),
   a shipping SSMS 22 extension. Key facts lifted from it (2026-07-15):
   - SDK-style csproj (`Microsoft.NET.Sdk`), TargetFramework `net48`, with
     `VSSDKBuildToolsAutoSetup`, `GeneratePkgDefFile`, `UseCodebase`,
     `VsixDeployOnDebug` properties + `<ProjectCapability Include="CreateVsixContainer" />`.
   - Packages: `Microsoft.VisualStudio.SDK` 17.14.x (ExcludeAssets=runtime),
     `Microsoft.VSSDK.BuildTools` 18.5.x, and optionally
     `Community.VisualStudio.Toolkit.17` for pleasant command/options wiring.
   - Manifest (PackageManifest v2 schema): InstallationTarget
     `Id="Microsoft.VisualStudio.Ssms" Version="[22.0,)"` declared TWICE,
     once with ProductArchitecture amd64 and once arm64; Prerequisite
     `Microsoft.VisualStudio.Component.CoreEditor [17.0,)`; Asset
     `Microsoft.VisualStudio.VsPackage` from PkgdefProjectOutputGroup.
   - AsyncPackage + same .vsct commands as SSMS18, referencing SSMSLib.
4. Deploy: SSMS 18 = manual Extensions-folder copy + `ssms.exe /setup`
   (`C:\Program Files (x86)\...\SSMS 18\Common7\IDE\Extensions\`), per
   windows-ssms-dev.md. SSMS 22 = the built .vsix INSTALLS directly
   (SSMS 22 honors VSIXes targeting Microsoft.VisualStudio.Ssms even
   though it has no Manage Extensions dialog); manual folder copy remains
   the fallback. Distribution beyond personal use exists for SSMS 22: the
   Open SSMS VSIX Gallery (https://ssmsgallery.azurewebsites.net/),
   published via the madskristensen/publish-vsixgallery GitHub Action -
   candidates: signed VSIX (ErikEJ uses Azure Trusted Signing).
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
