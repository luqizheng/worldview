using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using WorldView.Application.Bootstrap;
using WorldView.Domain.Persistence;

namespace WorldView.Infrastructure.Persistence.CoreDomain;

/// <summary>
/// Core Domain 模块迁移来源（P2-09-13 §2：模块拥有自己的迁移内容，
/// Application Bootstrap 只负责排序执行）。迁移 SQL 为 Domain 拥有的常量
/// （<see cref="CoreDomainSchema"/>），checksum 为脚本内容 SHA-256（小写十六进制），
/// 与 ledger 已应用记录比对实现 fail-closed。
/// </summary>
public sealed class CoreDomainMigrationSource : IMigrationSource
{
    public IReadOnlyList<ModuleMigration> DiscoverMigrations()
    {
        var migrations = new List<ModuleMigration>(CoreDomainSchema.Migrations.Count);
        foreach (var (migrationId, sql) in CoreDomainSchema.Migrations)
        {
            migrations.Add(new ModuleMigration
            {
                ModuleId = CoreDomainSchema.ModuleId,
                MigrationId = migrationId,
                Checksum = ComputeChecksum(sql),
                SqlScript = sql,
            });
        }

        return migrations;
    }

    internal static string ComputeChecksum(string sqlScript)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sqlScript));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }
}
