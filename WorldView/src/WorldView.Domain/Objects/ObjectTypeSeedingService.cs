using System;
using System.Collections.Generic;
using WorldView.Domain.Projects;

namespace WorldView.Domain.Objects;

/// <summary>
/// 内置 Object Type 播种服务（P2-09-18 §4.2 "Project 创建时 Core Domain 播种内置类型行"；
/// OD-4 建议案：Project 创建事务内播种）。类型行是 Project 级配置权威，不是可执行 schema。
/// </summary>
public static class ObjectTypeSeedingService
{
    /// <summary>
    /// 构建十内置类型种子行（全部 allocatable、最小分配格式，P2-09-21 V0.1）。
    /// 播种幂等性由 (project_id, type_key) UNIQUE 保证；本方法只构建行数据。
    /// </summary>
    public static IReadOnlyList<ObjectTypeRecord> BuildSeedRows(Guid projectId, long nowUtcMs)
    {
        var rows = new List<ObjectTypeRecord>(BuiltinObjectTypes.TypeKeys.Count);
        foreach (var typeKey in BuiltinObjectTypes.TypeKeys)
        {
            rows.Add(new ObjectTypeRecord
            {
                ObjectTypeId = Guid.NewGuid(),
                ProjectId = projectId,
                TypeKey = typeKey,
                DisplayName = BuiltinObjectTypes.DisplayNames[typeKey],
                Allocatable = true,
                AllocationFormatJson = ObjectAllocationFormat.MinimalDecimalJson,
                AllocationFormatVersion = ObjectAllocationFormat.VersionMinimalDecimal,
                CreatedAtUtcMs = nowUtcMs,
                UpdatedAtUtcMs = nowUtcMs,
            });
        }

        return rows;
    }

    /// <summary>构建完整 Project 创建计划（身份行 + 种子行，单事务写入）。</summary>
    public static ProjectCreationPlan BuildCreationPlan(ProjectRecord project) => ProjectCreationPlan.Create(project);
}
