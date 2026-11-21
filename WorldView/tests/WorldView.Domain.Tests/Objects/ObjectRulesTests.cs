using System;
using System.Linq;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using Xunit;

namespace WorldView.Domain.Tests.Objects;

/// <summary>
/// CD-02-B 领域规则单元测试：type_key/显示名规范化、R-scope 配对、身份级生命周期
/// 状态机、pin 校验（P2-09-26 V0.2 十类型值）、定义 schema 版本一致性。
/// </summary>
public sealed class ObjectRulesTests
{
    [Theory]
    [InlineData("landmass")]
    [InlineData("point_of_interest")]
    [InlineData("type_2")]
    public void NormalizeTypeKey_AcceptsLowercaseUnderscoreDigits(string key)
    {
        Assert.Equal(key, ObjectRules.NormalizeTypeKey(key));
        Assert.Equal("key", ObjectRules.NormalizeTypeKey("  key  "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Upper")]
    [InlineData("with space")]
    [InlineData("with-dash")]
    [InlineData("with.dot")]
    public void NormalizeTypeKey_RejectsInvalidKeys(string key)
    {
        Assert.Throws<ArgumentException>(() => ObjectRules.NormalizeTypeKey(key));
    }

    [Fact]
    public void NormalizeTypeKey_RejectsOver64Chars()
    {
        Assert.Throws<ArgumentException>(() => ObjectRules.NormalizeTypeKey(new string('a', 65)));
    }

    [Fact]
    public void ValidateScopePairing_ProjectSharedWithOwner_Rejected()
    {
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectRules.ValidateScopePairing(ObjectSharingScope.ProjectShared, Guid.NewGuid()));
    }

    [Fact]
    public void ValidateScopePairing_MapLocalWithoutOwner_Rejected()
    {
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectRules.ValidateScopePairing(ObjectSharingScope.MapLocal, null));
    }

    [Theory]
    [InlineData(ObjectSharingScope.ProjectShared, true)]
    [InlineData(ObjectSharingScope.MapLocal, false)]
    public void ValidateScopePairing_ValidPairings_Pass(ObjectSharingScope scope, bool shared)
    {
        ObjectRules.ValidateScopePairing(scope, shared ? null : Guid.NewGuid());
    }

    [Fact]
    public void Lifecycle_AllowedTransitions_Only()
    {
        Assert.True(ObjectRules.CanTransitionLifecycle(ObjectLifecycleStatus.Draft, ObjectLifecycleStatus.Active));
        Assert.True(ObjectRules.CanTransitionLifecycle(ObjectLifecycleStatus.Draft, ObjectLifecycleStatus.Abandoned));
        Assert.True(ObjectRules.CanTransitionLifecycle(ObjectLifecycleStatus.Active, ObjectLifecycleStatus.Retired));

        // 非法转换（枚举全组合差集）。
        Assert.False(ObjectRules.CanTransitionLifecycle(ObjectLifecycleStatus.Active, ObjectLifecycleStatus.Draft));
        Assert.False(ObjectRules.CanTransitionLifecycle(ObjectLifecycleStatus.Active, ObjectLifecycleStatus.Abandoned));
        Assert.False(ObjectRules.CanTransitionLifecycle(ObjectLifecycleStatus.Abandoned, ObjectLifecycleStatus.Active));
        Assert.False(ObjectRules.CanTransitionLifecycle(ObjectLifecycleStatus.Abandoned, ObjectLifecycleStatus.Draft));
        Assert.False(ObjectRules.CanTransitionLifecycle(ObjectLifecycleStatus.Retired, ObjectLifecycleStatus.Active));
        Assert.False(ObjectRules.CanTransitionLifecycle(ObjectLifecycleStatus.Draft, ObjectLifecycleStatus.Retired));
    }

    [Fact]
    public void ValidatePin_BuiltinValues_Pass()
    {
        foreach (var key in BuiltinObjectTypes.TypeKeys)
        {
            // 逐 descriptor 版本（十基础类型 =2；Building/Cave 采纳对 =1，P2-09-41 V0.4）。
            ObjectRules.ValidatePin(key, BuiltinObjectTypes.DescriptorSchemaVersionFor(key), BuiltinObjectTypes.SemanticsIdFor(key));
        }
    }

    [Fact]
    public void ValidatePin_WrongSchemaVersion_FailClosed()
    {
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectRules.ValidatePin("river", 3, BuiltinObjectTypes.SemanticsIdFor("river")));
    }

    [Fact]
    public void ValidatePin_WrongSemanticsId_FailClosed()
    {
        Assert.Throws<CoreDomainConflictException>(
            () => ObjectRules.ValidatePin("river", BuiltinObjectTypes.DescriptorSchemaVersion, "world-view.object-type.river.v2"));
    }

    [Fact]
    public void ValidatePin_UnknownTypeKey_FailClosed()
    {
        // CD-02-C 起 pin 校验经 registry：未知 type_key 以 CoreDomainConflictException
        // 包装 RegistryException（稳定错误码 unknown_type_key）。
        // （building 自 CD-02-E 起为采纳类型；此处用未注册键。）
        var ex = Assert.Throws<CoreDomainConflictException>(
            () => ObjectRules.ValidatePin("interior_space", 2, "world-view.object-type.interior_space.v1"));
        Assert.Contains("unknown_type_key", ex.Message);
    }

    [Fact]
    public void BuiltinCatalog_MatchesP2_09_26V0_2()
    {
        // 十基础 type_key 匹配 P2-09-21 V0.1 / P2-09-26 V0.2（顺序无关）；
        // Building/Cave 采纳对（CD-02-E）追加，共 12。
        var expected = new[]
        {
            "landmass", "mountain_range", "mountain_peak", "river", "lake",
            "terrain_region", "settlement", "road", "political_region", "point_of_interest",
        };
        Assert.Equal(12, BuiltinObjectTypes.TypeKeys.Count);
        Assert.Equal(expected.OrderBy(k => k, StringComparer.Ordinal),
            BuiltinObjectTypes.TypeKeys.Take(10).OrderBy(k => k, StringComparer.Ordinal));
        Assert.Contains("building", BuiltinObjectTypes.TypeKeys);
        Assert.Contains("cave_system", BuiltinObjectTypes.TypeKeys);

        // semantics_id 模板（world-view.object-type.<key>.v1）。
        Assert.Equal("world-view.object-type.landmass.v1", BuiltinObjectTypes.SemanticsIdFor("landmass"));
        Assert.Equal("world-view.object-type.point_of_interest.v1", BuiltinObjectTypes.SemanticsIdFor("point_of_interest"));
        Assert.Equal("world-view.object-type.building.v1", BuiltinObjectTypes.SemanticsIdFor("building"));
        Assert.Equal(2, BuiltinObjectTypes.DescriptorSchemaVersion); // 十基础类型

        // 采纳对逐 descriptor 版本（P2-09-41 V0.4 = 1）。
        Assert.Equal(1, BuiltinObjectTypes.DescriptorSchemaVersionFor("building"));
        Assert.Equal(1, BuiltinObjectTypes.DescriptorSchemaVersionFor("cave_system"));
    }

    [Fact]
    public void ValidateDefinitionSchemaVersion_Mismatch_FailClosed()
    {
        var identity = new ObjectRecord
        {
            ObjectIdentityId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ObjectTypeId = Guid.NewGuid(),
            ObjectTypeSchemaVersion = 2,
            ObjectTypeSemanticsId = "world-view.object-type.river.v1",
            SharingScope = ObjectSharingScope.ProjectShared,
            LifecycleStatus = ObjectLifecycleStatus.Active,
            CreatedAtUtcMs = 1,
            UpdatedAtUtcMs = 1,
        };
        ObjectRules.ValidateDefinitionSchemaVersion(identity, 2);
        Assert.Throws<CoreDomainConflictException>(() => ObjectRules.ValidateDefinitionSchemaVersion(identity, 3));
    }
}

/// <summary>Reservation 状态机（P2-09-04 §5/§7.6）。</summary>
public sealed class ReservationRulesTests
{
    [Fact]
    public void LegalTransitions_Only()
    {
        Assert.True(ReservationRules.CanTransition(ReservationState.Reserved, ReservationState.Bound));
        Assert.True(ReservationRules.CanTransition(ReservationState.Reserved, ReservationState.Abandoned));
        Assert.True(ReservationRules.CanTransition(ReservationState.Bound, ReservationState.Inserted));
        Assert.True(ReservationRules.CanTransition(ReservationState.Bound, ReservationState.Abandoned));
    }

    [Fact]
    public void IllegalTransitions_Rejected()
    {
        // 五条非法转换（Handoff §6）：Reserved→Inserted、终态重开、终态间转换。
        Assert.False(ReservationRules.CanTransition(ReservationState.Reserved, ReservationState.Inserted));
        Assert.False(ReservationRules.CanTransition(ReservationState.Bound, ReservationState.Reserved));
        Assert.False(ReservationRules.CanTransition(ReservationState.Inserted, ReservationState.Bound));
        Assert.False(ReservationRules.CanTransition(ReservationState.Inserted, ReservationState.Abandoned));
        Assert.False(ReservationRules.CanTransition(ReservationState.Abandoned, ReservationState.Bound));
        Assert.False(ReservationRules.CanTransition(ReservationState.Abandoned, ReservationState.Abandoned));
        Assert.False(ReservationRules.CanTransition(ReservationState.Inserted, ReservationState.Inserted));
    }

    [Fact]
    public void ValidateTransition_Illegal_Throws()
    {
        Assert.Throws<CoreDomainConflictException>(
            () => ReservationRules.ValidateTransition(ReservationState.Reserved, ReservationState.Inserted));
        Assert.Throws<CoreDomainConflictException>(
            () => ReservationRules.ValidateTransition(ReservationState.Inserted, ReservationState.Abandoned));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" req-1 ", "req-1")]
    public void NormalizeRequestKey_TrimsOrClears(string? input, string? expected)
    {
        Assert.Equal(expected, ReservationRules.NormalizeRequestKey(input));
    }
}

/// <summary>分配格式（OD-3 最小格式）。</summary>
public sealed class AllocationFormatTests
{
    [Theory]
    [InlineData(1, "1")]
    [InlineData(9, "9")]
    [InlineData(42, "42")]
    [InlineData(123456789, "123456789")]
    public void DeriveAllocatedIdValue_DecimalText(long sequence, string expected)
    {
        Assert.Equal(expected, ObjectAllocationFormat.DeriveAllocatedIdValue(sequence));
    }

    [Fact]
    public void DeriveAllocatedIdValue_ZeroOrNegative_Rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ObjectAllocationFormat.DeriveAllocatedIdValue(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ObjectAllocationFormat.DeriveAllocatedIdValue(-1));
    }
}
