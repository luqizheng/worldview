# World View P2-09-09A — Avalonia / .NET / SQLite Runtime Compatibility Assessment

- Date: 2026-11-21
- Status: **Compatibility direction confirmed by current official framework documentation; packaged application verification remains required**
- Scope: Resolve whether the proposed Microsoft.Data.Sqlite 11.x target framework conflicts with the confirmed C# + Avalonia desktop direction.

## 1. Finding

The framework compatibility concern is substantially resolved at the target-framework level:

- Avalonia 12 documentation states that desktop platforms (Windows, macOS, Linux) require .NET 8 or later and recommends .NET 10.
- Avalonia 12 dropped .NET Framework and .NET Standard support; .NET 10 is the recommended target.
- Microsoft.Data.Sqlite 11.0.0 targets `net10.0`.

Therefore, **Avalonia 12 + .NET 10 + Microsoft.Data.Sqlite 11.x is a compatible and coherent recommended target combination**. There is no need to downgrade the SQLite provider solely to avoid an Avalonia framework mismatch.

## 2. Recommended baseline

- UI framework: Avalonia 12.x (pin the selected stable patch at implementation time).
- Target framework: `net10.0` for the shared desktop application layer, with any platform-specific TFM/RID additions justified by actual packaging needs.
- SQLite provider: Microsoft.Data.Sqlite 11.x, with the default SQLitePCLRaw bundled native library.
- Persistence access: direct ADO.NET and explicit SQL repositories; no EF Core requirement in the critical commit path.

This is a technology compatibility recommendation, not an implementation authorization.

## 3. Remaining verification

Before freezing the provider package lock:

1. Verify the exact Avalonia 12 stable patch and its package dependency graph.
2. Build and launch Windows x64 and macOS arm64/x64 desktop packages as applicable.
3. Verify the bundled native SQLite library loads from the packaged application, not only from the development environment.
4. Verify FTS5, JSON1, R*Tree and foreign-key enforcement on each target package.
5. Run the shared-connection/shared-transaction integration proof through the packaged Microsoft.Data.Sqlite provider.
6. Verify publish mode, trimming/AOT decisions (if any), signing and application update packaging do not remove or break native SQLite assets.

## 4. Sources

- Avalonia 12 breaking changes and .NET support: https://docs.avaloniaui.net/docs/avalonia12-breaking-changes
- Avalonia supported platforms and desktop .NET requirements: https://docs.avaloniaui.net/docs/supported-platforms
- Microsoft.Data.Sqlite 11.0.0 NuGet target framework: https://www.nuget.org/packages/Microsoft.Data.Sqlite/11.0.0

## 5. Status

Target-framework compatibility: **RESOLVED — recommend Avalonia 12 + .NET 10 + Microsoft.Data.Sqlite 11.x**.

Native packaging, feature availability and transaction atomicity: **OPEN — require real Windows/macOS packaged-runtime verification**.