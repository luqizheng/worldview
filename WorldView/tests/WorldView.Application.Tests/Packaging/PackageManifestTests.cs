using System;
using WorldView.Application.Packaging;
using Xunit;

namespace WorldView.Application.Tests.Packaging;

public class PackageManifestTests
{
    [Fact]
    public void RoundTrip_CreatedManifestParses()
    {
        var manifest = PackageManifest.CreateV1(new DateTimeOffset(2026, 11, 21, 8, 0, 0, TimeSpan.Zero));
        var json = manifest.ToJson();

        var parse = PackageManifest.TryParse(json);

        Assert.True(parse.Success);
        Assert.Equal(1, parse.Manifest!.PackageFormatVersion);
        Assert.Equal(ProjectPackageFormat.DatabaseFileName, parse.Manifest.DatabaseRelativePath);
        Assert.Equal(manifest.CreatedAtUtc, parse.Manifest.CreatedAtUtc);
    }

    [Fact]
    public void Parse_MalformedJsonRejected()
    {
        var parse = PackageManifest.TryParse("{ not json ");

        Assert.False(parse.Success);
        Assert.Equal(PackageIssueCode.ManifestMalformed, parse.FailureCode);
    }

    [Fact]
    public void Parse_MissingFieldsRejected()
    {
        Assert.False(PackageManifest.TryParse("{}").Success);
        Assert.False(PackageManifest.TryParse("{\"packageFormatVersion\":1}").Success);
    }

    [Fact]
    public void Parse_UnsupportedNewerVersionRejected()
    {
        var json = "{\"packageFormatVersion\":99,\"database\":{\"relativePath\":\"project.sqlite\"},\"createdAtUtc\":\"2026-11-21T00:00:00Z\"}";

        var parse = PackageManifest.TryParse(json);

        Assert.False(parse.Success);
        Assert.Equal(PackageIssueCode.ManifestUnsupportedVersion, parse.FailureCode);
    }

    [Theory]
    [InlineData("../outside.sqlite")]
    [InlineData("a/../../outside.sqlite")]
    [InlineData("C:\\abs\\project.sqlite")]
    [InlineData("/abs/project.sqlite")]
    [InlineData("")]
    public void PathConfinement_UnsafePathsRejected(string relativePath)
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wv-manifest-tests");
        Assert.False(ProjectPackageStructure.IsRelativePathConfinedToPackage(root, relativePath));
    }

    [Fact]
    public void PathConfinement_SafeRelativePathAccepted()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wv-manifest-tests");
        Assert.True(ProjectPackageStructure.IsRelativePathConfinedToPackage(root, "project.sqlite"));
        Assert.True(ProjectPackageStructure.IsRelativePathConfinedToPackage(root, "data/project.sqlite"));
    }

    [Fact]
    public void WithPackageSuffix_AppendsOnlyWhenMissing()
    {
        Assert.Equal("我的世界.wvproject", ProjectPackageStructure.WithPackageSuffix("我的世界"));
        Assert.Equal("我的世界.wvproject", ProjectPackageStructure.WithPackageSuffix("我的世界.wvproject"));
    }
}
