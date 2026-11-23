# CD-02-G2 — Asset Byte Verification Report V0.1

- **Baseline:** G2 implementation branch based on G1 head `ab6d00f7afc13ba352df0c3b92eecd3a146afd99`
- **Branch:** `feat/cd-02-g2-asset-byte-verification`
- **Verified head:** `f0c3ca01e3c97427dc6191ac498e51d4620c317e`
- **Pull request:** [#2 — require byte-verification evidence for Verified state](https://github.com/luqizheng/worldview/pull/2) (draft; not merged)
- **Verdict:** PASS — implementation, negative persistence guards and configured Windows/macOS CI matrix verified

## 1. Scope delivered

### 1.1 Real Asset Store byte verification

`VerifyAssetIntegrityUseCase` now:

1. Reads the authoritative Asset metadata from the current Project database.
2. Opens the immutable content-addressed bytes through `IAssetStore.OpenReadAsync`.
3. Computes SHA-256 and actual byte length from the stream.
4. Records:
   - `Missing` when the content-addressed payload does not exist;
   - `Verified` only when both SHA-256 and byte length match the metadata;
   - `Corrupt` when either digest or byte length differs.
5. Returns an explicit result with outcome and diagnostic context. An unknown Asset returns `AssetNotFound` without mutating persistence.

### 1.2 Verified-state persistence boundary

The former unrestricted `UpdateAssetIntegrityState` port operation has been removed.

- `RecordAssetIntegrityFailure` accepts only `Corrupt` or `Missing`; it rejects `Verified` and `Unverified`.
- `MarkAssetVerified` requires an `AssetVerificationEvidence` value containing expected hash, actual hash, expected byte length and actual byte length.
- Before writing `verified`, the SQLite adapter validates the evidence shape and equality, opens the coordinated write transaction, re-reads the current Asset metadata, checks the SHA-256 algorithm, expected hash and expected byte length against the current row, and only then updates the integrity state.
- This closes the previous accidental bypass where any caller could pass `AssetIntegrityState.Verified` to a generic state-update method.

### 1.3 Test and compatibility updates

- Added real-SQLite verification tests for valid bytes, missing bytes, tampered bytes and unknown Asset IDs.
- Added persistence negative tests for mismatched actual hash, stale expected metadata and attempts to write Verified through the failure-only operation.
- Existing Asset persistence and Map Editor integration tests now use the constrained verification contract.
- The in-memory Domain test double implements the same state-transition contract.
- Corrected a pre-existing concurrency-test upper bound: four workers executing 25 rounds each can perform 13 even-indexed writes per worker, so the correct maximum is 52 (not integer-truncated 50).

## 2. CI verification

GitHub Actions run: [44975176429](https://github.com/luqizheng/worldview/actions/runs/44975176429)

| Platform | Debug build | Debug tests | Release build | Release tests |
|---|---:|---:|---:|---:|
| Windows | PASS | PASS — 393/393 | PASS | PASS — 393/393 |
| macOS | PASS | PASS — 393/393 | PASS | PASS — 393/393 |

All four test configurations reported 0 failed and 0 skipped. This is an actual run against the repository's configured .NET 11 CI matrix.

## 3. Explicit limitations

1. **Not a cryptographic attestation:** `AssetVerificationEvidence` is checked against itself and the current database metadata. Arbitrary malicious in-process code with direct access to the persistence port could fabricate matching values. The API is an application contract and misuse guard, not an OS/process security boundary.
2. **Explicit operation:** Asset registration does not automatically read and verify the bytes. The caller must invoke `VerifyAssetIntegrityUseCase` before relying on Verified state.
3. **I/O errors:** a missing content result is recorded as Missing. Other Asset Store I/O exceptions propagate; they do not get misclassified as Missing or Corrupt.
4. **Geometry manifest generalization remains separate:** G2 does not introduce a generic `geometry_components` contract for every Object Type. G1's transactional manifest/link enforcement remains limited to descriptors with the adopted Building/Cave typed aggregate schemas.

## 4. Status and next work

- G2 implementation and Windows/macOS Debug/Release CI: **PASS**.
- PR #2 is a draft and remains unmerged.
- Next Core Domain hardening items remain the generic Object Type Geometry Manifest contract (after descriptor contract review) and the P2-09-12 provider transaction-proof suite. The P2-09-12 V0.6 document is a test specification, not evidence that all TX-01 through TX-71 tests have been implemented or executed.
