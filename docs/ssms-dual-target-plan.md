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
3. DONE 2026-07-16 — created `RightWaySqlFormatter.SSMS22` by CLONING the
   working SSMS18 project and applying ONLY the async delta (recovered from
   the historical `PoorMansTSqlFormatterVSPackage2019` AsyncPackage in git).
   Chosen over a modern-template rewrite because the SSMS 22 load failure was
   never a toolchain problem (see step 4): the ActivityLog showed VS17 reached
   the autoload REQUEST for our package using the packages.config / Shell.15.0
   / VSSDK-BuildTools-17.12 / embedded-EnvDTE / net472 / AnyCPU toolchain — so
   registration + pkgdef parsing already worked on VS17, and only the loading
   model needed changing. What differs from SSMS18:
   - `AsyncPackage` (not `Package`), `[PackageRegistration(..., AllowsBackgroundLoading = true)]`,
     `[ProvideAutoLoad(..., PackageAutoLoadFlags.BackgroundLoad)]`, `InitializeAsync`
     + `SwitchToMainThreadAsync`. Drops the SSMS-2015 `SkipLoading` reg hack.
   - Fresh distinct identity: pkg `{e857c020-...}`, cmdset `{1a2afa6c-...}`,
     ProjectGuid `{CACE487F-...}` — no collision with the SSMS18 registration.
   - Manifest InstallationTarget `Microsoft.VisualStudio.Ssms [21.0,)`, declared
     twice (ProductArchitecture amd64 + arm64); Prerequisite CoreEditor [17.0,).
   - Added a `Microsoft.VisualStudio.Threading` ref (JoinableTaskFactory) and a
     `ForceEmbedEnvDTE` post-RAR target (see step 4's EnvDTE fix).
   THEN REBUILT to the shipping-quality template 2026-07-16 (Jeremy chose this
   for gallery publishing): the project was converted IN PLACE to SDK-style
   (`Microsoft.NET.Sdk`, TFM `net48`, `Microsoft.VisualStudio.SDK` 17.14 +
   `Microsoft.VSSDK.BuildTools` 18.5), modeled on ErikEJ's SqlAnalyzerSsms
   (https://github.com/ErikEJ/SqlServer.Rules/tree/master/tools/SqlAnalyzerSsms).
   Same folder / GUIDs / identity `{e857c020-...}` / `.vsct` / AsyncPackage
   class; only the toolchain modernized to bind the NATIVE VS17 shell (not the
   Shell.15.0 compat assembly the packages.config build rode) and target
   `[22.0,)` amd64+arm64. The `Microsoft.VisualStudio.Threading` ref and
   `ForceEmbedEnvDTE` target from the packages.config build were DROPPED — see
   step 4's EnvDTE note for why the SDK-17 build needed a different (cleaner)
   EnvDTE strategy.
4. Deploy: SSMS 18 = manual Extensions-folder copy + `ssms.exe /setup`
   (`C:\Program Files (x86)\...\SSMS 18\Common7\IDE\Extensions\`), per
   windows-ssms-dev.md. SSMS 22 = the built .vsix INSTALLS directly
   (SSMS 22 honors VSIXes targeting Microsoft.VisualStudio.Ssms even
   though it has no Manage Extensions dialog); manual folder copy remains
   the fallback. Distribution beyond personal use exists for SSMS 22: the
   Open SSMS VSIX Gallery (https://ssmsgallery.azurewebsites.net/),
   published via the madskristensen/publish-vsixgallery GitHub Action -
   candidates: signed VSIX (ErikEJ uses Azure Trusted Signing).
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
   SSMS 22: DONE 2026-07-16 — deploys, loads, and formats (whole document +
   selection); options dialog OK; exactly one set of menu items. Deploy the
   extracted VSIX payload into
   `...\SSMS 22\Release\Common7\IDE\Extensions\RightWaySqlFormatter.SSMS22\`,
   then run `SSMS.exe /setup` **elevated**. ActivityLog for SSMS 22 is at
   `%AppData%\Microsoft\SSMS\22.0_<hash>\ActivityLog.xml` (VS17 isolation) —
   parse it as UTF-16 XML (grep sees only nulls). Two blockers surfaced and
   were fixed (shell integration only, no formatter change):
   - **Synchronous autoload rejected.** VS17 IGNORES sync-autoload packages
     (ActivityLog: AutoLoadManager "ignored because package does not support
     background loading" + SyncAutoLoadedExtensions "synchronous autoload ...
     deprecated"). `Initialize()` never ran → both commands stayed at their
     `.vsct` DefaultDisabled (greyed out). Fix: `AsyncPackage` +
     `AllowsBackgroundLoading` + `BackgroundLoad` autoload — the pkgdef then
     emits `AllowsBackgroundLoad=dword:1` and the autoload value `dword:2`.
   - **EnvDTE type-identity mismatch.** Once it loaded, invoking Format threw
     `MissingMethodException: FormatSqlInTextDoc(EnvDTE80.DTE2)`. SSMSLib
     EMBEDS `DTE2`; the package passed a plain-PIA `DTE2`, and on the VS17
     runtime the two `DTE2` types did not unify (they do on SSMS 18 only
     because its GAC PIA identity happens to match the vendored embed). Fix:
     a `ForceEmbedEnvDTE` target (`AfterTargets="ResolveAssemblyReferences"`)
     flips `EmbedInteropTypes=true` on the EnvDTE/EnvDTE80 `ReferencePath`
     items, defeating the SDK meta-package's non-embedded copy that shadows
     the direct `<Reference>` flag. Now BOTH the package AND SSMSLib embed the
     same classic 8.0.0.0 interop types → identical `[TypeIdentifier]` scope
     GUID → `DTE2` unifies by construction, independent of the shell's EnvDTE.
     Verified at artifact level: SSMS22.dll references no EnvDTE/EnvDTE80/Interop.
     (This is the robust general fix; the SSMS18 package relies on GAC luck.)
   - **EnvDTE, take 2 — the SDK-17 rebuild.** When SSMS22 was rebuilt on the
     VS17 SDK (step 3), `ForceEmbedEnvDTE` no longer worked: the SDK 17.x
     meta-package's MODERN EnvDTE type-forwards `DTE2` to
     `Microsoft.VisualStudio.Interop`, so it cannot be embedded (CS1747/CS1759),
     and injecting the vendored classic instead collides with Interop's `DTE2`
     (CS0433) — and Interop can't be aliased away because it also houses
     `VSConstants`. Fixed at the ARCHITECTURE level instead: added
     `GenericVSHelper.FormatSqlInTextDoc(object)` to SSMSLib (a COM
     QueryInterface cast to its embedded `DTE2`). The SSMS22 package now hands
     the DTE across as `object`, so NO interop type crosses the assembly
     boundary — it uses the modern SDK EnvDTE cleanly with zero vendoring /
     embedding, and there is no cross-assembly unification to get right. The
     overload is additive; the SSMS18 package keeps calling the typed `DTE2`
     overload and is untouched. This is the cleaner general pattern; the
     SSMS18/packages.config embed dance above stays only because SSMS 18 lacks
     Interop 17 and must embed the classic PIA.
5. Add both projects to `RightWaySqlFormatter.slnx` (Windows-only solution);
   NoSSMS.slnx stays untouched.
6. Update README project table + windows-ssms-dev.md with what actually
   worked (paths, InstallationTarget values, gotchas).
7. Publish SSMS 22 to the community Open VSIX Gallery (Jeremy, 2026-07-16).
   Research (primary-sourced): publishing is a token-less single HTTP POST of
   the `.vsix`; **no signing required** (signing is only end-user SmartScreen
   trust, decoupled); the galleries read metadata from the `.vsix` manifest
   only — they do not care how it was built. Two galleries: the main
   `vsixgallery.com` and the SSMS-filtered `ssmsgallery.azurewebsites.net`.
   - CI: `.github/workflows/publish-ssms22.yml` — `workflow_dispatch` (manual),
     `windows-latest`, `microsoft/setup-msbuild`, stamps the manifest Identity
     version `2.0.<run_number>`, msbuilds the SSMS22 project, publishes via
     `madskristensen/publish-vsixgallery@v1` to BOTH galleries (second step
     overrides `gallery-url`). Token-less; no secrets.
   - End-user install (SSMS 21/22 has no Extensions UI): explicit
     `...\SSMS 22\...\IDE\VSIXInstaller.exe <file>.vsix`; double-clicking routes
     to Visual Studio's installer if VS is present, so document the SSMS-path
     invocation. Uninstall by manifest `Identity Id`
     (`VSIXInstaller.exe /uninstall:e857c020-...`). AnyCPU + amd64/arm64 targets
     are the real must-haves (ARM64 SSMS runs an ARM64 runtime).
   - Optional later polish: code-sign the VSIX (Azure Trusted Signing, as
     ErikEJ does) for SmartScreen trust; a tag/release trigger instead of
     manual dispatch. Not required to publish.

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
