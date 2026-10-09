using Godot;
using WorldView.Domain;

namespace WorldView.Ui;

/// <summary>
/// 新增地图对话框（W5 / C-D1-弹窗）。
/// <para>
/// UI：类型 OptionButton（World / City）+ 名称 LineEdit + Ok / Cancel。
/// 调 <c>ProjectManagementService.AddMap(type, name, parent)</c>。
/// </para>
/// <para>
/// 设计要点：
/// <list type="bullet">
///   <item>不引入新的快捷键；只走菜单点击（参 <c>ProjectChromeEntry</c>）。</item>
///   <item>City 必须有父 World：<c>parentMapId</c> 必传（由调用方按 <c>EditorContext.ActiveMapId</c> 决定）。</item>
///   <item>World 也允许传 <c>parentMapId</c>（<c>AddMap</c> 留作可空，按 MVP 仅 World→无父）。</item>
///   <item>Exclusive = false 与现有 <c>FileDialog</c> 共存（详见 <c>MapSettingsDialog</c> 注释）。</item>
/// </list>
/// </para>
/// </summary>
public partial class AddMapDialog : AcceptDialog
{
    private OptionButton? _typeButton;
    private LineEdit? _nameEdit;
    private Label? _errorLabel;

    private string? _parentMapId;
    private bool _wired;

    /// <summary>
    /// 配置：父地图 id（用于 City 必填）。<c>Setup</c> 后 <see cref="PopupCentered"/> 即可显示。
    /// </summary>
    public void Setup(string? parentMapId)
    {
        if (!_wired)
        {
            BuildUi();
            Confirmed += OnConfirmed;
            CloseRequested += OnCanceled;
            Canceled += OnCanceled;
            _wired = true;
        }
        _parentMapId = parentMapId;
        _errorLabel!.Text = string.Empty;
        _nameEdit!.Text = string.Empty;
    }

    /// <summary>当前选中的地图类型。</summary>
    public MapType SelectedType =>
        _typeButton?.Selected == 1 ? MapType.City : MapType.World;

    /// <summary>当前输入的名称（trim 后；避免与 Godot Node.Name 冲突改名 MapName）。</summary>
    public string MapName => _nameEdit?.Text?.Trim() ?? string.Empty;

    private void BuildUi()
    {
        Title = "新增地图";
        Size = new Vector2I(420, 200);
        Unresizable = false;
        Exclusive = false; // 与 FileDialog 共存

        var root = new VBoxContainer { Name = "Body" };
        root.AddThemeConstantOverride("separation", 6);
        AddChild(root);

        // 类型行
        var typeRow = new HBoxContainer { Name = "TypeRow" };
        typeRow.AddChild(new Label { Text = "类型：" });
        _typeButton = new OptionButton { Name = "TypeButton" };
        _typeButton.AddItem("世界地图 (World)", 0);
        _typeButton.AddItem("城市地图 (City)", 1);
        _typeButton.Selected = 0;
        typeRow.AddChild(_typeButton);
        root.AddChild(typeRow);

        // 名称行
        var nameRow = new HBoxContainer { Name = "NameRow" };
        nameRow.AddChild(new Label { Text = "名称：" });
        _nameEdit = new LineEdit { Name = "NameEdit", PlaceholderText = "例如：北境之地" };
        _nameEdit.CustomMinimumSize = new Vector2(220, 0);
        nameRow.AddChild(_nameEdit);
        root.AddChild(nameRow);

        // 错误行
        _errorLabel = new Label
        {
            Name = "ErrorLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1, 0.4f, 0.4f),
        };
        root.AddChild(_errorLabel);
    }

    private void OnConfirmed()
    {
        // 用 MapName 属性（LineEdit 文本），不要用 Godot Node.Name（本节点名=类名）
        var name = MapName;
        if (string.IsNullOrWhiteSpace(name))
        {
            _errorLabel!.Text = "地图名不能为空。";
            // 取消 Confirmed 处理（让对话框保持打开）
            Hide();
            PopupCentered();
            return;
        }

        // City 必须有父 World
        if (SelectedType == MapType.City && string.IsNullOrEmpty(_parentMapId))
        {
            _errorLabel!.Text = "请先在世界中选中一张父地图，再新增 City。";
            Hide();
            PopupCentered();
            return;
        }

        try
        {
            var map = WorldView.ProjectManagement.ProjectManagementService.AddMap(
                SelectedType, name, _parentMapId);
            if (map is null)
            {
                _errorLabel!.Text = "新增失败：未加载工程。";
                Hide();
                PopupCentered();
            }
        }
        catch (WorldView.ProjectManagement.ProjectException ex)
        {
            _errorLabel!.Text = ex.Message;
            Hide();
            PopupCentered();
        }
    }

    private void OnCanceled()
    {
        _errorLabel!.Text = string.Empty;
        _nameEdit!.Text = string.Empty;
    }
}
