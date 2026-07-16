# Windows VM: building the project and deploying to SSMS 22

Playbook for working on the SSMS plugin from a Windows machine (tested target:
SSMS 22) using Claude Code. The macOS-side rules in AGENTS.md/CLAUDE.md apply
unchanged; this covers what is Windows-only.

## Reality check: SSMS 21/22 extension support

SSMS 21+ is built on the Visual Studio 2022 shell (64-bit, .NET Framework),
but **Microsoft does not officially support extensions in SSMS 21/22** — there
is no Extension Manager UI, and Microsoft states forced extensions are
unsupported ([SSMS FAQ](https://learn.microsoft.com/en-us/ssms/faq),
[Developer Community: SSMS v22 missing Extension Manager](https://developercommunity.visualstudio.com/t/SSMS-v22-Missing-the-Extension-Manager/11056569)).

The community approach still works and is what this playbook uses: a classic
.NET Framework 4.7.2 VSIX-style package, deployed by copying files into SSMS's
Extensions folder and rebuilding the package cache. Known constraints
(confirmed in [this MS Q&A thread](https://learn.microsoft.com/en-us/answers/questions/5665791/will-an-ssms-22-extension-built-for-x64-run-on-ssm)):

- Build managed assemblies **AnyCPU** with Prefer32Bit off (x64-only managed
  assemblies fail to load on ARM64 hosts, where the CLR is ARM64 even though
  SSMS.exe runs under x64 emulation).
- If the package doesn't load, `ActivityLog.xml` (run `ssms.exe /log`) is the
  primary diagnostic; no "BeginLoad" entry means registration/manifest issues.
- An SSMS update can break or remove the extension at any time. It's a
  personal-use deployment, not a distributable.

## One-time machine setup

```powershell
# 1. Git with long paths (test data lives deep under bin\...\Data\)
winget install Git.Git
git config --global core.longpaths true
# As Administrator, enable OS-wide long paths:
Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" -Name LongPathsEnabled -Value 1

# 2. .NET 10 SDK
winget install Microsoft.DotNet.SDK.10

# 3. Visual Studio 2022 (Community is fine) with:
#    - ".NET desktop development" workload
#    - "Visual Studio extension development" workload (VSSDK)
#    - ".NET Framework 4.7.2 targeting pack" (individual component)
winget install Microsoft.VisualStudio.2022.Community

# 4. Claude Code
winget install Anthropic.ClaudeCode   # or: npm install -g @anthropic-ai/claude-code

# 5. SSMS 18.12.1 (final 18.x) for testing the SSMS18 package - installs
#    side by side with SSMS 21/22 (different install models; the 21+
#    version/channel-uniqueness rule doesn't apply against 18.x).
#    Download: https://learn.microsoft.com/en-us/ssms/install/install
#    (release history) / KB5014879. Installs to
#    C:\Program Files (x86)\Microsoft SQL Server Management Studio 18.
#    Niggles: last-registered SSMS wins the .sql file association; the
#    18.x installer may request a reboot.

# 6. Clone SHALLOW in the path sense - C:\src keeps MAX_PATH at bay
git clone https://github.com/BadMonkeySoftware/right-way-sql-formatter.git C:\src\rwsql
```

Run Claude Code from a **Developer PowerShell for VS 2022** window (so
`msbuild` is on PATH) in `C:\src\rwsql`. Claude reads CLAUDE.md/AGENTS.md
automatically; all expected-file and test-baseline rules apply on Windows too.

## Build and verify (before touching SSMS)

```powershell
# Core library + CLI + tests - must be green before any plugin work
dotnet test RightWaySqlFormatter.NoSSMS.slnx
# Baseline: 0 failed. Total/skipped counters vary by runner
# (sandbox counter: 589 total / 10 skipped; VS runner discovers more).
```

## Building the SSMS package

The SSMS projects are classic VSSDK projects: build them with **msbuild, not
`dotnet build`** (VSSDK targets need full MSBuild from VS).

```powershell
msbuild RightWaySqlFormatter.slnx /restore /p:Configuration=Release
# Or just one package (restore first if the obj\ cache is cold):
msbuild RightWaySqlFormatter.SSMS18\RightWaySqlFormatter.SSMS18.csproj /t:Restore;Build /p:Configuration=Release
msbuild RightWaySqlFormatter.SSMS22\RightWaySqlFormatter.SSMS22.csproj /t:Restore;Build /p:Configuration=Release
```

**Two package projects, one per shell (DONE 2026-07-16).** `SSMS18` is a
synchronous `Package` for SSMS 17–20; `SSMS22` is an `AsyncPackage` for
SSMS 21/22. Both build with the same toolchain (packages.config +
`Microsoft.VisualStudio.SDK` 15.0.1 + `Microsoft.VSSDK.BuildTools` 17.12,
Shell.15.0, net472, AnyCPU) and share the shell-agnostic SSMSLib. The two
things the VS 2022 shell forced (neither is a toolchain change — SSMS22 was
cloned from SSMS18 with only these deltas):

- **AsyncPackage + background load.** VS17 refuses synchronous autoload, so
  SSMS22 uses `AsyncPackage`, `[PackageRegistration(AllowsBackgroundLoading = true)]`,
  and `[ProvideAutoLoad(..., PackageAutoLoadFlags.BackgroundLoad)]`. Verify the
  generated `.pkgdef` has `AllowsBackgroundLoad=dword:00000001` and an
  `AutoLoadPackages` entry of `dword:00000002`.
- **Embed EnvDTE in the package too.** SSMSLib embeds `EnvDTE`/`EnvDTE80`
  (No-PIA); the package must embed the *same* classic 8.0.0.0 interop so the
  `DTE2` types unify at runtime (else `MissingMethodException` on the first
  format). The SDK meta-package ships a non-embedded EnvDTE that shadows the
  direct `<Reference EmbedInteropTypes>` flag, so SSMS22 adds a `ForceEmbedEnvDTE`
  target (`AfterTargets="ResolveAssemblyReferences"`) that flips
  `EmbedInteropTypes=true` on the resolved EnvDTE/EnvDTE80 `ReferencePath`
  items. **Gate:** reflect the built package DLL — it must reference no
  `EnvDTE`/`EnvDTE80`/`Microsoft.VisualStudio.Interop`.

Upstream demand signal for SSMS 22 support:
[#297](https://github.com/TaoK/PoorMansTSqlFormatter/issues/297)/[#301](https://github.com/TaoK/PoorMansTSqlFormatter/issues/301).

## Deploying to SSMS 22 (manual, unsupported-by-Microsoft path)

SSMS 22 installs at
`C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE`
(confirm on the VM; SSMS 21 used the same pattern with `21`).

1. Close SSMS (**all** instances — SSMS 18 and 22 share the `Ssms` process name).
2. Extract the built `.vsix` (it is a zip) into
   `...\Common7\IDE\Extensions\RightWaySqlFormatter.SSMS22\`: the plugin DLLs,
   `*.pkgdef`, `extension.vsixmanifest`, and the `es/` `fr/` satellites. Remove
   any prior deployment folder first so stale pkgdef GUIDs don't linger.
3. From an **elevated** prompt, rebuild the extension/package cache:

   ```powershell
   & "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\SSMS.exe" /setup
   ```

4. Start SSMS (`SSMS.exe /log`) and check Tools ▸ **Format T-SQL Code** /
   **T-SQL Formatting Options...** — they should be enabled and format.
5. If the commands are missing or greyed out, inspect the ActivityLog. For
   SSMS 22 it is at
   `%AppData%\Microsoft\SSMS\22.0_<hash>\ActivityLog.xml` (VS17 isolation root,
   **not** the old `SQL Server Management Studio\<version>` path). It is
   **UTF-16** — `grep` sees only null bytes; parse it as XML (e.g. PowerShell
   `[xml]`). Signatures seen in practice:
   - `AutoLoadManager … ignored because package does not support background loading`
     + `SyncAutoLoadedExtensions … synchronous autoload … deprecated` = the
     package is a synchronous `Package`; it needs AsyncPackage + background load.
   - Commands load and enable but formatting throws
     `MissingMethodException: FormatSqlInTextDoc(EnvDTE80.DTE2)` = the EnvDTE
     embed mismatch above (package didn't embed EnvDTE).

To remove: delete the folder and run `SSMS.exe /setup` again.

## Rules of engagement for Claude Code on the VM

- Same repo rules as everywhere: never modify expected Data files without
  sign-off; run `dotnet test RightWaySqlFormatter.NoSSMS.slnx` before calling
  anything done; formatting behavior is additive-only.
- The SSMS package shares the core library: formatter changes belong on the
  normal (macOS/CI) workflow; the VM is for shell integration only.
- Commit from the VM normally; the repo's git identity should match
  `Jeremy Adams <jeremy.adams@badmonkeysoftware.com>`.
