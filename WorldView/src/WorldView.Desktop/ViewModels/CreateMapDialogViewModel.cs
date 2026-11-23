using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorldView.Application.UseCases;

namespace WorldView.Desktop.ViewModels;

/// <summary>
/// 创建地图对话框 ViewModel（WP-20a）：名称输入 + 经 <see cref="CreateMapUseCase"/>
/// 的正式 Core Domain 创建。成功后请求关闭对话框（携带成功标记，父窗口据此刷新
/// 地图列表；不自动打开工作区）；失败在对话框内显示可理解错误（同名/空白名称）；
/// 取消不调用创建用例，不产生任何持久化变化。提交期间禁止重复提交。
/// </summary>
public partial class CreateMapDialogViewModel : ObservableObject
{
    private readonly CreateMapUseCase _createMapUseCase;

    /// <summary>请求关闭对话框（参数 = 是否已成功创建；窗口层据此关闭并决定刷新）。</summary>
    public event Action<bool>? RequestClose;

    public CreateMapDialogViewModel(CreateMapUseCase createMapUseCase)
    {
        _createMapUseCase = createMapUseCase;
    }

    /// <summary>地图名称输入（用户原文；规范化由用例负责）。</summary>
    [ObservableProperty]
    private string _mapName = string.Empty;

    /// <summary>校验/创建失败提示（成功路径不显示）。</summary>
    [ObservableProperty]
    private string? _errorText;

    /// <summary>是否正在提交（防重复提交；同时驱动 CreateCommand 可用性）。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isSubmitting;

    /// <summary>提交创建（正式 Core Domain 路径；成功关闭对话框，失败显示错误留在对话框）。</summary>
    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        if (IsSubmitting)
        {
            return;
        }

        IsSubmitting = true;
        try
        {
            var result = await _createMapUseCase.ExecuteAsync(MapName, cancellationToken).ConfigureAwait(true);
            if (result.Success)
            {
                ErrorText = null;
                RequestClose?.Invoke(true);
                return;
            }

            ErrorText = result.Diagnostic ?? "创建失败。";
        }
        catch (OperationCanceledException)
        {
            ErrorText = "创建已取消。";
        }
        catch (Exception ex)
        {
            // 防御性兜底：用例层已把失败折叠为结果，此分支理论上不可达。
            ErrorText = $"创建失败：{ex.Message}";
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private bool CanCreate() => !IsSubmitting;

    /// <summary>取消：关闭对话框且不产生任何持久化变化。</summary>
    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
