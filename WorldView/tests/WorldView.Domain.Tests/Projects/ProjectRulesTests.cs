using System;
using WorldView.Domain.Projects;

namespace WorldView.Domain.Tests.Projects;

public class ProjectRulesTests
{
    [Fact]
    public void NormalizeName_TrimsAndRejectsBlank()
    {
        Assert.Equal("My World", ProjectRules.NormalizeName("  My World\t"));
        Assert.Throws<ArgumentException>(() => ProjectRules.NormalizeName("   "));
        Assert.Throws<ArgumentException>(() => ProjectRules.NormalizeName(null));
        Assert.Throws<ArgumentException>(() => ProjectRules.NormalizeName(""));
    }

    [Fact]
    public void CreateProject_GeneratesUniqueIdentityAndTrimsName()
    {
        var now = 1_763_000_000_000L;
        var first = ProjectRules.CreateProject("  Alpha  ", now);
        var second = ProjectRules.CreateProject("Beta", now);

        Assert.NotEqual(first.ProjectId, second.ProjectId);
        Assert.Equal("Alpha", first.ProjectName);
        Assert.Equal(ProjectRules.CurrentProjectFormatVersion, first.ProjectFormatVersion);
        Assert.Equal(now, first.CreatedAtUtcMs);
        Assert.Equal(now, first.UpdatedAtUtcMs);
    }

    [Fact]
    public void ValidateIdentityRows_Empty_IsNotEstablished()
    {
        var (outcome, record) = ProjectRules.ValidateIdentityRows(Array.Empty<RawProjectIdentityRow>());
        Assert.Equal(ProjectIdentityOutcome.NotEstablished, outcome);
        Assert.Null(record);
    }

    [Fact]
    public void ValidateIdentityRows_SingleValidRow_IsOk()
    {
        var id = Guid.NewGuid();
        var (outcome, record) = ProjectRules.ValidateIdentityRows(new[]
        {
            new RawProjectIdentityRow
            {
                ProjectIdText = id.ToString("D"),
                ProjectName = "Alpha",
                ProjectFormatVersion = 1,
                CreatedAtUtcMs = 1L,
                UpdatedAtUtcMs = 1L,
            },
        });

        Assert.Equal(ProjectIdentityOutcome.Ok, outcome);
        Assert.NotNull(record);
        Assert.Equal(id, record.ProjectId);
        Assert.Equal("Alpha", record.ProjectName);
    }

    [Fact]
    public void ValidateIdentityRows_MultipleRows_IsMultipleProjects()
    {
        var row = new RawProjectIdentityRow
        {
            ProjectIdText = Guid.NewGuid().ToString("D"),
            ProjectName = "A",
            ProjectFormatVersion = 1,
            CreatedAtUtcMs = 1L,
            UpdatedAtUtcMs = 1L,
        };

        var (outcome, record) = ProjectRules.ValidateIdentityRows(new[] { row, row with { ProjectName = "B" } });
        Assert.Equal(ProjectIdentityOutcome.MultipleProjects, outcome);
        Assert.Null(record);
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("123")]
    [InlineData("")]
    public void ValidateIdentityRows_UnparsableId_IsInvalid(string idText)
    {
        var (outcome, record) = ProjectRules.ValidateIdentityRows(new[]
        {
            new RawProjectIdentityRow
            {
                ProjectIdText = idText,
                ProjectName = "Alpha",
                ProjectFormatVersion = 1,
                CreatedAtUtcMs = 1L,
                UpdatedAtUtcMs = 1L,
            },
        });

        Assert.Equal(ProjectIdentityOutcome.Invalid, outcome);
        Assert.Null(record);
    }

    [Fact]
    public void ValidateIdentityRows_BlankNameOrBadVersion_IsInvalid()
    {
        var id = Guid.NewGuid();
        var blankName = new RawProjectIdentityRow
        {
            ProjectIdText = id.ToString("D"),
            ProjectName = "   ",
            ProjectFormatVersion = 1,
            CreatedAtUtcMs = 1L,
            UpdatedAtUtcMs = 1L,
        };
        Assert.Equal(ProjectIdentityOutcome.Invalid, ProjectRules.ValidateIdentityRows(new[] { blankName }).Outcome);

        var badVersion = blankName with { ProjectName = "Alpha", ProjectFormatVersion = 0 };
        Assert.Equal(ProjectIdentityOutcome.Invalid, ProjectRules.ValidateIdentityRows(new[] { badVersion }).Outcome);
    }
}
