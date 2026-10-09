namespace WorldView.ProjectManagement;

/// <summary>
/// 抽象用户配置目录定位。生产实现走 <c>Godot.ProjectSettings.GlobalizePath("user://")</c>
/// （避免静态依赖 Godot，通过反射获取），失败时回落到 <c>%APPDATA%/WorldView</c>。
/// </summary>
public interface IAppDataLocator
{
    /// <summary>返回一个已存在的绝对路径；本接口不负责创建目录。</summary>
    string GetUserConfigDir();
}
