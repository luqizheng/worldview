using System;

namespace WorldView.ProjectManagement;

/// <summary>
/// 项目管理模块统一的错误码（覆盖规格 §7 全部错误）。
/// </summary>
public enum ProjectErrorCode
{
    InvalidProject,
    ProjectAlreadyOpen,
    ProjectHasUnsavedChanges,
    MapNotFound,
    SaveFailed,
    BackupFailed,
    RecoveryFailed,
    RecentProjectsCorrupted,
    DirectoryNotEmpty,
    DirectoryNotEmptyForCreate,
    NameConflict,
}

/// <summary>
/// 项目管理模块统一异常。所有抛给上层（包括 UI）的可预期错误都应包装成此类型，
/// 上层可读取 <see cref="Code"/> 判定原因，避免字符串匹配。
/// </summary>
public class ProjectException : Exception
{
    public ProjectErrorCode Code { get; }

    public ProjectException(ProjectErrorCode code, string message, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
    }

    // ---- 静态工厂 ----

    public static ProjectException Invalid(string message)
        => new(ProjectErrorCode.InvalidProject, message);

    public static ProjectException AlreadyOpen(string message)
        => new(ProjectErrorCode.ProjectAlreadyOpen, message);

    public static ProjectException ProjectHasUnsavedChanges()
        => new(ProjectErrorCode.ProjectHasUnsavedChanges, "工程存在未保存的修改，无法执行当前操作。");

    public static ProjectException MapNotFound(string mapId)
        => new(ProjectErrorCode.MapNotFound, $"找不到地图：{mapId}");

    public static ProjectException SaveFailed(string message, Exception? inner = null)
        => new(ProjectErrorCode.SaveFailed, message, inner);

    public static ProjectException BackupFailed(string message, Exception? inner = null)
        => new(ProjectErrorCode.BackupFailed, message, inner);

    public static ProjectException RecoveryFailed(string message, Exception? inner = null)
        => new(ProjectErrorCode.RecoveryFailed, message, inner);

    public static ProjectException RecentProjectsCorrupted(string message)
        => new(ProjectErrorCode.RecentProjectsCorrupted, message);

    public static ProjectException DirectoryNotEmptyForCreate(string message)
        => new(ProjectErrorCode.DirectoryNotEmptyForCreate, message);

    public static ProjectException NameConflict(string message)
        => new(ProjectErrorCode.NameConflict, message);

    public static ProjectException DirectoryNotEmpty(string message)
        => new(ProjectErrorCode.DirectoryNotEmpty, message);
}
