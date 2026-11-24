using System;
using System.Linq;
using System.Reflection;
using WorldView.Presentation.Identity;
using Xunit;

namespace WorldView.Presentation.Tests.Architecture;

/// <summary>架构边界：Presentation 程序集不得引用 Avalonia/Skia/数据库/ORM/Infrastructure/Desktop。</summary>
public sealed class PresentationBoundaryTests
{
    private static readonly string[] ForbiddenNameParts =
    {
        "Avalonia",
        "Skia",
        "Sqlite",
        "EntityFramework",
        "Dapper",
        "Npgsql",
        "Microsoft.Data",
        "WorldView.Infrastructure",
        "WorldView.Desktop",
        "CommunityToolkit",
    };

    [Fact]
    public void PresentationAssembly_HasOnlyApplicationAsProjectReference()
    {
        var referenced = typeof(PresentationContext).Assembly
            .GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToList();

        // WorldView 系引用只允许 Application（直接项目引用）与 Domain
        // （HistoricalTime 等 typed 值经 Application 传递依赖暴露；依赖方向
        // Presentation → Application → Domain 合法且单向）。
        var worldViewRefs = referenced.Where(name => name.StartsWith("WorldView", StringComparison.Ordinal)).ToList();
        Assert.Contains("WorldView.Application", worldViewRefs);
        Assert.Contains("WorldView.Domain", worldViewRefs);
        Assert.Equal(2, worldViewRefs.Count);

        // 禁止的依赖一律不得作为直接引用出现。
        Assert.DoesNotContain(referenced, name =>
            ForbiddenNameParts.Any(forbidden => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void PresentationPublicApi_DoesNotExposeInfrastructureOrUiTypes()
    {
        // Presentation 公开 API 不得暴露 Infrastructure/桌面/绘制技术栈类型。
        var assembly = typeof(PresentationContext).Assembly;

        foreach (var type in assembly.GetTypes().Where(t => t.IsPublic))
        {
            var fullName = type.FullName ?? string.Empty;
            Assert.False(
                fullName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
                || fullName.Contains("Avalonia", StringComparison.OrdinalIgnoreCase)
                || fullName.Contains("Skia", StringComparison.OrdinalIgnoreCase),
                $"{fullName} 名称暴露违禁技术栈。");

            var exposedTypes = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetParameters().Select(p => p.ParameterType))
                .Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.PropertyType))
                .ToList();

            foreach (var exposed in exposedTypes)
            {
                var exposedAssembly = exposed.Assembly.GetName().Name ?? string.Empty;
                Assert.False(
                    exposedAssembly.StartsWith("WorldView.Infrastructure", StringComparison.Ordinal)
                    || exposedAssembly.Contains("Avalonia", StringComparison.OrdinalIgnoreCase)
                    || exposedAssembly.Contains("Skia", StringComparison.OrdinalIgnoreCase),
                    $"{type.Name} 公开暴露 {exposed.FullName}（{exposedAssembly}）——违反 PRA-01 §1.2 依赖禁令。");
            }
        }
    }
}
