using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorldView.Application.Abstractions;
using WorldView.Application.Projects;

namespace WorldView.Desktop.ViewModels;

/// <summary>
/// 主窗口 ViewModel（MVVM 基线示例）。
/// 仅通过 Application 层接口访问用例，不直接接触领域模型与仓储。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IMapProjectService _projectService;
    private int _sampleCounter;

    public MainViewModel(IMapProjectService projectService)
    {
        _projectService = projectService;
    }

    /// <summary>状态栏文本。</summary>
    [ObservableProperty]
    private string _statusText = "就绪";

    /// <summary>最近一次创建的项目名称。</summary>
    [ObservableProperty]
    private string _lastCreatedProjectName = "";

    /// <summary>当前项目总数。</summary>
    [ObservableProperty]
    private int _projectCount;

    /// <summary>
    /// 创建一个示例项目，验证 UI → Application → Domain → Infrastructure 完整链路。
    /// </summary>
    [RelayCommand]
    private void CreateSampleProject()
    {
        _sampleCounter++;
        var result = _projectService.CreateMapProject(new CreateMapProjectCommand($"示例项目 {_sampleCounter:000}"));

        if (result.Success && result.Project is not null)
        {
            LastCreatedProjectName = result.Project.Name;
            ProjectCount = _projectService.ListProjects().Count;
            StatusText = $"已创建「{result.Project.Name}」，当前共 {ProjectCount} 个项目";
        }
        else
        {
            StatusText = $"创建失败：{result.Error}";
        }
    }
}
