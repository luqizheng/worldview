namespace WorldView.Application.Abstractions;

/// <summary>按数据库文件路径创建包数据库访问实例（Infrastructure 以 Microsoft.Data.Sqlite 实现）。</summary>
public interface IPackageDatabaseFactory
{
    /// <summary>为指定数据库文件创建访问实例（不打开；调用方 OpenAsync）。</summary>
    IPackageDatabase CreateForFile(string databasePath);
}
