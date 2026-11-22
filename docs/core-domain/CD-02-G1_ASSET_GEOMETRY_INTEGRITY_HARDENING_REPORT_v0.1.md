# CD-02-G1 — Asset / Geometry Integrity Hardening Report V0.1

- **Baseline:** CD-02 final implementation 7e176dd
- **Branch:** fix/cd-02-g1-asset-geometry-integrity
- **Status:** implementation committed; GitHub Actions verification pending
- **Scope:** close concrete Asset / Geometry integrity gaps found during independent CD-02 acceptance review

## 1. Changes delivered

### 1.1 Verified-only formal Asset links

- AssetService.LinkDefinitionAsset now rejects every state except Verified.
- The SQLite adapter independently re-reads Object lifecycle, Asset integrity, Object Type and the current Definition inside the same SqliteConnectionAccess write transaction that inserts the typed link.
- Direct port callers cannot bypass the active-Object / verified-Asset / declared-role checks.

### 1.2 Building / Cave manifest-to-link consistency

For descriptors with typed aggregate schemas (Building and Cave):

- A formal typed Asset link requires exactly one current geometry_components entry matching asset_id, geometry_role and the authoritative core_asset.content_hash.
- Definition CAS rechecks all existing formal links in its write transaction. A Definition edit cannot remove, duplicate or change the Asset/role/hash manifest entry for an existing link.
- ME-04 integration evidence now creates the footprint manifest before linking, checks missing/mismatched manifest rejection, preserves component identity across Definition CAS, and verifies that removing a linked component is rejected.

This does not add a component table or change Object identity semantics.

### 1.3 JSON Schema fail-closed recursion

- JsonSchemaSubset.EnsureSupportedKeywords now recursively inspects $defs schemas as well as properties, items and composition branches.
- Added a negative test where an unsupported keyword exists only inside a referenced $defs schema.

### 1.4 Schema and status documentation

- Corrected the schema count: 10 migrations create 16 tables (CD-01: 6, CD-02-B: 8, CD-02-D: 2).
- Marked the pre-CD-02 Building/Cave gap audit as historical.
- Updated IMPLEMENTATION-LOG.md maturity and persistence rows.
- Corrected the CoreDomainSchema.cs header comment so it no longer lists implemented Asset tables as excluded.

## 2. Tests added or strengthened

- Real-SQLite test for Unverified rejection through both Domain service and direct adapter call.
- Real-SQLite test for adapter-side active lifecycle revalidation.
- ME-04 end-to-end test for missing manifest, wrong hash, matching manifest, stable component identity and Definition CAS protection.
- JSON Schema $defs unsupported-keyword fail-closed test.

**No test result is claimed in this report yet.** The new GitHub Actions workflow runs full Debug and Release build/test on Windows and macOS with .NET 11. CI must complete successfully before this stage is accepted.

## 2.1 First CI run findings and remediation

GitHub Actions run 16 completed on both Windows and macOS. Both Debug builds succeeded, but the Debug test step failed:

- Windows: 382/383 passed. The existing OrphanScan test fixture attempted to create a formal link while its Asset was still Unverified. The new Verified-only rule correctly rejected it. The fixture now explicitly transitions the referenced Asset to Verified.
- macOS: 360/383 passed. In addition to the same OrphanScan fixture, 22 Application tests failed because the in-memory test filesystem discarded the leading Unix root separator while constructing directory nodes. The path tree therefore stored absolute paths as relative paths. CreateDirectory now preserves Path.GetPathRoot and builds descendants from that root.
- Both runners compiled successfully. Release steps were skipped because Debug tests failed.

Remediation commits:
- 443b4d3 — mark the OrphanScan referenced Asset fixture Verified.
- 5b76a36 — preserve Unix root in the in-memory filesystem test double.

A new CI run for the updated PR head is required before acceptance. No passing result is claimed yet.

## 3. Explicit remaining limitations

1. UpdateAssetIntegrityState remains a persistence port operation. This hardening requires Verified for formal links but does not yet provide a complete user-facing IAssetStore byte-verification workflow; that integration must calculate the hash from actual immutable bytes before setting Verified.
2. Manifest/link transaction enforcement is enabled for typed aggregate descriptors (Building/Cave). Simple descriptors do not yet have a common typed geometry_components payload schema; their geometry publication contract needs a separate registry/schema decision rather than an invented generic payload field.
3. macOS verification is now included in the new CI matrix; the existing P2-09-12 full transaction-proof suite remains outside this patch.

## 4. Git / verification

- This branch is based on the accepted CD-02 final report commit and does not modify master directly.
- The branch is intended for review through a pull request.
- Final acceptance depends on the attached GitHub Actions Debug and Release results.
