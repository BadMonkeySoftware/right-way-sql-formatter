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

# 5. Clone SHALLOW in the path sense - C:\src keeps MAX_PATH at bay
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
# Or just the package:
msbuild RightWaySqlFormatter.SSMSPackage\RightWaySqlFormatter.SSMSPackage.csproj /restore /p:Configuration=Release
```

**Expected migration work (first task on this VM):** the package was written
against the SSMS 18-era shell (VS2019 SDK). For the VS 2022 shell it will
likely need: VSSDK 17.x package references, `PlatformTarget` AnyCPU
(Prefer32Bit off), and a `source.extension.vsixmanifest` InstallationTarget
that matches the SSMS 22 shell identity. Treat upstream issues
[#297](https://github.com/TaoK/PoorMansTSqlFormatter/issues/297)/[#301](https://github.com/TaoK/PoorMansTSqlFormatter/issues/301)
as the demand signal for this work.

## Deploying to SSMS 22 (manual, unsupported-by-Microsoft path)

SSMS 22 installs at
`C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE`
(confirm on the VM; SSMS 21 used the same pattern with `21`).

1. Close SSMS.
2. Create `...\Common7\IDE\Extensions\RightWaySqlFormatter\` and copy the
   package build output into it: the plugin DLLs, `*.pkgdef`, and
   `extension.vsixmanifest`.
3. From an **elevated** prompt, rebuild the extension/package cache:

   ```powershell
   & "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\ssms.exe" /setup
   ```

4. Start SSMS and check for the formatter menu/commands.
5. If nothing appears: run `ssms.exe /log`, then inspect `ActivityLog.xml`
   (`%AppData%\Microsoft\SQL Server Management Studio\<version>\ActivityLog.xml`).
   No "BeginLoad" for the package = manifest/InstallationTarget or pkgdef
   problem; a load exception = dependency/architecture problem.

To remove: delete the folder and run `ssms.exe /setup` again.

## Rules of engagement for Claude Code on the VM

- Same repo rules as everywhere: never modify expected Data files without
  sign-off; run `dotnet test RightWaySqlFormatter.NoSSMS.slnx` before calling
  anything done; formatting behavior is additive-only.
- The SSMS package shares the core library: formatter changes belong on the
  normal (macOS/CI) workflow; the VM is for shell integration only.
- Commit from the VM normally; the repo's git identity should match
  `Jeremy Adams <jeremy.adams@badmonkeysoftware.com>`.
