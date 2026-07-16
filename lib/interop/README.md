# Vendored EnvDTE PIAs

`EnvDTE.dll` / `EnvDTE80.dll` are Microsoft's **classic v8.0.0.0** EnvDTE Primary
Interop Assemblies (from NuGet `EnvDTE` 8.0.2 / `EnvDTE80` 8.0.3, which ship the
same v8.0.0.0 embeddable PIAs SSMS/VS install into the GAC).

They are referenced by the SSMS plugin projects with `EmbedInteropTypes=true`, so
the COM interface definitions are **baked into our own assemblies at build time** —
nothing here ships in the extension, and at runtime the CLR binds to the live
`DTE` object by COM GUID.

Why vendored (not a `PackageReference`):
- The modern EnvDTE 17.x packages forward their base interfaces to
  `Microsoft.VisualStudio.Interop 17.0.0.0` (a VS2022 assembly SSMS 18 lacks) —
  that dependency is what broke loading on SSMS 18.
- `EmbedInteropTypes` is unreliable via `PackageReference`; a direct `<Reference>`
  with a stable `HintPath` embeds reliably and works for both the packages.config
  (SSMS18 package) and SDK-style (SSMSLib) project shapes.

These are redistributable interop assemblies; they only matter for the Windows-only
SSMS plugin builds.
