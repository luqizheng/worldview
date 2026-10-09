using Godot;
using WorldView.Editor;
using WorldView.ProjectManagement;

namespace WorldView.Ui;

/// <summary>
/// 32px 顶部条左侧的「工程按钮」(W5 / C-L1)。
/// <para>
/// 文字 = 当前工程名（或"未打开工程"）；点击弹 5 项 PopupMenu：
/// 0=打开项目 / 1=切换工程 / 2=新建项目 / 3=新增地图 / 4=关闭项目。
/// </para>
/// <para>
/// 强约束：
/// <list type="bullet">
///   <item><b>C-L1</b> 32px 顶部条第一项，左对齐，与 <c>Breadcrumb</c> 同条。</item>
///   <item><b>C-L1-挤压</b> 此按钮属 TopStrip 内的子节点，不影响 WorldCanvas.position=(0,32) 锚点。</item>
///   <item><b>C-M跨</b> "切换工程" 走 <c>ProjectManagementService.SwitchProject</c>。</item>
///   <item><b>C-M内</b> "新增地图" 不动 recents / last_opened_at（服务层保证）。</item>
///   <item><b>C-D1-弹窗</b> "新增地图" 弹 <c>AddMapDialog</c>（类型 + 命名）。</item>
///   <item><b>C-S2</b> 打开失败 → ConfirmationDialog 提示"该路径不是工程，是否新建项目到此处"。</item>
/// </list>
/// </para>
/// </summary>
public partial class ProjectChromeEntry : Button
{
    /// <summary>工程按钮 PopupMenu 5 项的 id 枚举（与 <c>_OnMenuItem</c> 联动）。</summary>
    private enum MenuItem
    {
        OpenProject = 0,
        SwitchProject = 1,
        NewProject = 2,
        AddMap = 3,
        CloseProject = 4,
    }

    [Export] public NodePath? EditorContextPath { get; set; }

    private EditorContext? _context;
    private PopupMenu? _menu;
    private FileDialog? _openFileDialog;
    private FileDialog? _switchFileDialog;
    private FileDialog? _parentPickerDialog;
    private AcceptDialog? _newProjectDialog;
    private LineEdit? _newProjectNameEdit;
    private LineEdit? _newProjectParentEdit;
    private AddMapDialog? _addMapDialog;
    private ConfirmationDialog? _unsavedDialog;

    // 待切换的目标路径（UnsavedDialog 决策后使用）
    private string? _pendingSwitchTarget;

    private bool _wired;

    public override void _Ready()
    {
        if (!_wired)
        {
            BuildChildren();
            Pressed += OnPressed;
            if (_menu is not null) _menu.IdPressed += OnMenuItem;
            _context = ResolveContext();
            if (_context is not null)
            {
                _context.MapChanged += OnContextChanged;
            }
            _wired = true;
        }
        RefreshState();
    }

    public override void _ExitTree()
    {
        if (_context is not null)
        {
            _context.MapChanged -= OnContextChanged;
        }
    }

    // ──── 公开 API ───────────────────────────────────────────────────────────

    /// <summary>
    /// 设置工程显示名。null = 未加载工程。
    /// </summary>
    public void SetProject(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            Text = "未打开工程";
            Disabled = false;
        }
        else
        {
            Text = name;
            Disabled = false;
        }
        UpdateMenuItemStates();
    }

    /// <summary>
    /// 重读 <see cref="EditorContext"/> 状态刷新文字与菜单项 Disabled。
    /// </summary>
    public void RefreshState()
    {
        var ctx = _context ?? ResolveContext();
        _context = ctx;
        if (ctx is null)
        {
            SetProject(null);
            return;
        }
        if (!ctx.IsLoaded || ctx.World is null)
        {
            SetProject(null);
        }
        else
        {
            SetProject(ctx.World.Name);
        }
    }

    // ──── 内部 ───────────────────────────────────────────────────────────

    private EditorContext? ResolveContext()
    {
        if (EditorContextPath is { } path && !path.IsEmpty)
        {
            var node = GetNodeOrNull<EditorContext>(path);
            if (node is not null) return node;
        }
        return EditorContext.Instance;
    }

    private void BuildChildren()
    {
        // PopupMenu 5 项
        _menu = new PopupMenu { Name = "ChromeMenu" };
        _menu.AddItem("打开项目…", (int)MenuItem.OpenProject);
        _menu.AddItem("切换工程…", (int)MenuItem.SwitchProject);
        _menu.AddItem("新建项目…", (int)MenuItem.NewProject);
        _menu.AddItem("新增地图…", (int)MenuItem.AddMap);
        _menu.AddItem("关闭项目", (int)MenuItem.CloseProject);
        AddChild(_menu);

        // 打开工程：FileDialog（OpenDir 模式）
        _openFileDialog = new FileDialog
        {
            Name = "OpenFileDialog",
            FileMode = FileDialog.FileModeEnum.OpenDir,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "打开工程目录",
            Size = new Vector2I(720, 480),
        };
        _openFileDialog.DirSelected += path => OnOpenDirSelected(path);
        AddChild(_openFileDialog);

        // 切换工程：FileDialog（OpenDir 模式，复用 _switchFileDialog 单独走 C-M跨）
        _switchFileDialog = new FileDialog
        {
            Name = "SwitchFileDialog",
            FileMode = FileDialog.FileModeEnum.OpenDir,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "切换工程目录",
            Size = new Vector2I(720, 480),
        };
        _switchFileDialog.DirSelected += path => OnSwitchDirSelected(path);
        AddChild(_switchFileDialog);

        // 新建项目：AcceptDialog + 父目录 LineEdit + 名称 LineEdit
        _newProjectDialog = new AcceptDialog
        {
            Name = "NewProjectDialog",
            Title = "新建项目",
            Size = new Vector2I(480, 180),
            Exclusive = false,
        };
        var newRoot = new VBoxContainer();
        newRoot.AddThemeConstantOverride("separation", 6);
        var parentRow = new HBoxContainer();
        parentRow.AddChild(new Label { Text = "父目录：" });
        _newProjectParentEdit = new LineEdit { PlaceholderText = "例如 D:/kudi-projects", CustomMinimumSize = new Vector2(300, 0) };
        parentRow.AddChild(_newProjectParentEdit);
        var pickBtn = new Button { Text = "…" };
        pickBtn.Pressed += () => PickParentDir();
        parentRow.AddChild(pickBtn);
        newRoot.AddChild(parentRow);

        var nameRow = new HBoxContainer();
        nameRow.AddChild(new Label { Text = "工程名：" });
        _newProjectNameEdit = new LineEdit { PlaceholderText = "例如 my_world", CustomMinimumSize = new Vector2(300, 0) };
        nameRow.AddChild(_newProjectNameEdit);
        newRoot.AddChild(nameRow);

        _newProjectDialog.AddChild(newRoot);
        _newProjectDialog.Confirmed += OnNewProjectConfirmed;
        AddChild(_newProjectDialog);

        // 新增地图
        _addMapDialog = new AddMapDialog();
        AddChild(_addMapDialog);

        // 父目录选择（独立 FileDialog，不污染主打开工程事件订阅）
        _parentPickerDialog = new FileDialog
        {
            Name = "ParentPickerDialog",
            FileMode = FileDialog.FileModeEnum.OpenDir,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "选择父目录",
            Size = new Vector2I(720, 480),
        };
        _parentPickerDialog.DirSelected += OnParentDirPicked;
        AddChild(_parentPickerDialog);

        // 未保存确认
        _unsavedDialog = new ConfirmationDialog
        {
            Name = "UnsavedDialog",
            Title = "工程存在未保存修改",
            DialogText = "工程存在未保存修改。是否先保存？",
            Exclusive = false,
            OkButtonText = "保存",
        };
        // 取消按钮：右侧"放弃修改"和"取消"
        _unsavedDialog.AddButton("放弃修改", false, "discard");
        _unsavedDialog.AddButton("取消", false, "cancel");
        // 默认 OK（"保存"）走 Confirmed
        _unsavedDialog.Confirmed += OnUnsavedSavePressed;
        // 自定义按钮（"放弃修改"/"取消"）走 CustomAction
        _unsavedDialog.CustomAction += OnUnsavedCustomAction;
        _unsavedDialog.Canceled += () => _pendingSwitchTarget = null;
        AddChild(_unsavedDialog);
    }

    private void UpdateMenuItemStates()
    {
        if (_menu is null) return;
        var loaded = _context is { IsLoaded: true } && _context.World is not null;
        _menu.SetItemDisabled((int)MenuItem.SwitchProject, false); // 切换始终可点（即使未加载）
        _menu.SetItemDisabled((int)MenuItem.NewProject, false); // 新建始终可点
        _menu.SetItemDisabled((int)MenuItem.AddMap, !loaded);
        _menu.SetItemDisabled((int)MenuItem.CloseProject, !loaded);
    }

    private void OnPressed()
    {
        if (_menu is null) return;
        // 弹出在按钮下方
        var globalPos = GlobalPosition + new Vector2(0, Size.Y);
        _menu.Popup(new Rect2I((Vector2I)globalPos, _menu.Size));
    }

    private void OnMenuItem(long id)
    {
        switch ((MenuItem)id)
        {
            case MenuItem.OpenProject:
                _openFileDialog?.PopupCentered();
                break;
            case MenuItem.SwitchProject:
                _switchFileDialog?.PopupCentered();
                break;
            case MenuItem.NewProject:
                if (_newProjectDialog is not null)
                {
                    if (_newProjectParentEdit is not null) _newProjectParentEdit.Text = string.Empty;
                    if (_newProjectNameEdit is not null) _newProjectNameEdit.Text = string.Empty;
                    _newProjectDialog.PopupCentered();
                }
                break;
            case MenuItem.AddMap:
                if (_addMapDialog is not null && _context is not null)
                {
                    _addMapDialog.Setup(_context.ActiveMapId);
                    _addMapDialog.PopupCentered();
                }
                break;
            case MenuItem.CloseProject:
                OnCloseProjectRequested();
                break;
        }
    }

    private void OnOpenDirSelected(string path)
    {
        try
        {
            ProjectManagementService.OpenProject(path);
        }
        catch (ProjectException ex)
        {
            // C-S2：提示用户"该路径不是工程，是否新建项目到此处"
            ShowInvalidProjectDialog(path, ex.Message);
        }
    }

    private void OnSwitchDirSelected(string path)
    {
        // C-M跨：切换 = 关闭 + 打开。脏状态下弹 UnsavedDialog 让用户决策；
        // 无脏 / 用户已决策再走 SwitchProject。
        try
        {
            ProjectManagementService.SwitchProject(path);
        }
        catch (ProjectException ex)
        {
            if (ex.Code == ProjectErrorCode.ProjectHasUnsavedChanges)
            {
                _pendingSwitchTarget = path;
                _unsavedDialog?.PopupCentered();
            }
            else
            {
                ShowInvalidProjectDialog(path, ex.Message);
            }
        }
    }

    private void OnCloseProjectRequested()
    {
        if (_context is null || !_context.IsLoaded) return;

        // 脏状态走 UnsavedDialog；无脏状态直接关
        try
        {
            ProjectManagementService.CloseProject();
            RefreshState();
        }
        catch (ProjectException ex)
        {
            _context?.NotifyHint($"关闭失败：{ex.Message}");
        }
    }

    private void OnNewProjectConfirmed()
    {
        var parent = _newProjectParentEdit?.Text?.Trim() ?? string.Empty;
        var name = _newProjectNameEdit?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            _context?.NotifyHint("请填写父目录和工程名。");
            return;
        }

        try
        {
            ProjectManagementService.NewProject(parent, name);
            // NewProject 内部已 OpenProject，刷新一次状态
            RefreshState();
        }
        catch (ProjectException ex)
        {
            _context?.NotifyHint($"新建项目失败：{ex.Message}");
        }
    }

    private void PickParentDir()
    {
        // 独立 FileDialog，不污染主打开工程的事件订阅
        if (_parentPickerDialog is null) return;
        _parentPickerDialog.PopupCentered();
    }

    private void OnParentDirPicked(string path)
    {
        if (_newProjectParentEdit is not null)
        {
            _newProjectParentEdit.Text = path;
        }
    }

    private void ShowInvalidProjectDialog(string path, string message)
    {
        var dlg = new ConfirmationDialog
        {
            Title = "该路径不是工程",
            DialogText = $"{message}\n\n是否在此处新建项目？",
            Exclusive = false,
        };
        dlg.Confirmed += () =>
        {
            // 切换到 NewProject 对话框并预填父目录
            if (_newProjectParentEdit is not null) _newProjectParentEdit.Text = path;
            _newProjectDialog?.PopupCentered();
            dlg.QueueFree();
        };
        dlg.Canceled += () => dlg.QueueFree();
        AddChild(dlg);
        dlg.PopupCentered();
    }

    private void OnContextChanged()
    {
        RefreshState();
    }

    private void OnUnsavedCustomAction(StringName action)
    {
        // 自定义按钮："放弃修改" / "取消"
        var target = _pendingSwitchTarget;
        _pendingSwitchTarget = null;
        switch (action.ToString())
        {
            case "discard":
                if (target is not null) ContinueSwitch(target, ProjectCloseDecision.CloseWithoutSaving);
                break;
            case "cancel":
            default:
                // 取消
                break;
        }
    }

    private void OnUnsavedSavePressed()
    {
        // 默认 OK 按钮（OkButtonText = "保存"）走 Confirmed
        var target = _pendingSwitchTarget;
        _pendingSwitchTarget = null;
        if (_context is null || target is null) return;
        try
        {
            _context.SaveProject();
        }
        catch (System.Exception ex)
        {
            _context.NotifyHint($"保存失败：{ex.Message}");
            return;
        }
        ContinueSwitch(target);
    }

    private void ContinueSwitch(string target, ProjectCloseDecision? decision = null)
    {
        try
        {
            if (decision is { } d)
            {
                ProjectManagementService.SwitchProject(target, d);
            }
            else
            {
                ProjectManagementService.SwitchProject(target);
            }
        }
        catch (ProjectException ex)
        {
            ShowInvalidProjectDialog(target, ex.Message);
        }
    }
}
