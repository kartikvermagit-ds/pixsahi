# Contributing to PixSahi

Small fixes, usability improvements, and reproducible bug reports are welcome. Discuss larger features in an issue first.

## Build and test

On Windows, clone the repository and run:

```powershell
.\scripts\build.ps1 -Test -Package
```

This uses the .NET Framework compiler supplied with Windows, with no NuGet restore. Keep code compatible with the supplied C# compiler unless a toolchain change has been discussed separately.

| File | Purpose |
| --- | --- |
| `src/Imaging.cs` | Image loading, orientation, resizing, encoding, and safe saving |
| `src/PixSahi.cs` | WinForms UI, crop interactions, and asynchronous preparation |
| `src/Tests.cs` | Generated fixtures, image checks, and UI-state tests |
| `src/app.manifest` | Windows compatibility, DPI settings, and normal user permissions |
| `scripts/build.ps1` | Build, test, and release packaging |

Generated binaries and QA files belong in ignored `dist/` and `artifacts/`. Release assets are published separately from source commits.

## What to verify

Run `-Test` for dimensions, byte limits, compression, crop/fit, orientation, corruption handling, exact saved bytes, source preservation, and stale-result protection. Test images are generated locally and do not use personal photos.

For visual or export changes, also manually check:

1. Open-file and save-file dialogs, including overwrite confirmation.
2. Crop dragging, zoom, reset, rotation, and fit-with-margins behavior.
3. Readability at low JPG quality and correct errors for impossible limits.
4. Window resizing and display scaling, including mixed-DPI setups.
5. The chosen output against the relevant portal's current rules.

Do not claim portal acceptance based only on dimensions and file size. Do not add official presets without a documented source and date. Avoid uploads or analytics without an explicit product discussion: local image processing is central to this app.

## Pull requests and issues

Explain the user-visible behavior, include relevant tests, and attach a screenshot for UI changes. Use synthetic sample images. Never include personal signatures, identity documents, credentials, or private machine paths in issues or commits.

Contributions are made under the repository's MIT license.
