using System;
using WorldView.Application.Packaging;

namespace WorldView.Application.Fields;

/// <summary>Stable error codes for import/export access policy decisions (ME-05-12 §4).</summary>
public static class FieldPackagePolicyErrorCodes
{
    /// <summary>
    /// The package profile scope is <c>golden_fixture_only</c> and the caller-provided policy
    /// does not explicitly allow fixture profiles. Default production behavior is fail-closed.
    /// </summary>
    public const string GoldenFixtureProfileRejected = "WVPK_POLICY_GOLDEN_FIXTURE_ONLY";
}

/// <summary>
/// Explicit, instance-scoped access policy for Field Package import/export. The default
/// state is the production gate: <c>golden_fixture_only</c> encoding profiles are rejected
/// for import and export until a caller constructs a policy that explicitly allows them
/// (tests and explicitly authorized fixture imports). There is deliberately no static or
/// global switch: every service instance receives its policy at construction, so production
/// wiring cannot be flipped at runtime by unrelated code (ME-05-12 §4).
/// </summary>
public sealed record FieldPackagePolicy
{
    /// <summary>
    /// When true, packages and Field Revisions declaring
    /// <c>value_encoding.profile_scope = golden_fixture_only</c> may be imported and exported.
    /// Production profiles are always allowed and require no policy.
    /// </summary>
    public bool AllowGoldenFixtureOnlyProfiles { get; init; }
}

/// <summary>Shared fail-closed gate for the manifest's declared profile scope.</summary>
internal static class FieldPackagePolicyGuard
{
    /// <summary>
    /// Enforces the profile-scope gate. <c>production</c> passes unconditionally;
    /// <c>golden_fixture_only</c> passes only with an explicitly allowing policy; any other
    /// scope value fails closed even though manifest validation already restricts the
    /// vocabulary. The failure is a typed, stable policy error.
    /// </summary>
    public static void EnsureProfileScopeAllowed(
        string profileScope,
        FieldPackagePolicy policy,
        string operation)
    {
        if (string.Equals(profileScope, "production", StringComparison.Ordinal))
            return;
        if (string.Equals(profileScope, "golden_fixture_only", StringComparison.Ordinal)
            && policy.AllowGoldenFixtureOnlyProfiles)
            return;

        throw new FieldPackageValidationException(
            FieldPackagePolicyErrorCodes.GoldenFixtureProfileRejected,
            $"{operation} rejected by the production profile gate: value_encoding.profile_scope " +
            $"'{profileScope}' is not a production encoding and the supplied policy does not " +
            "explicitly allow golden-fixture profiles.");
    }
}
