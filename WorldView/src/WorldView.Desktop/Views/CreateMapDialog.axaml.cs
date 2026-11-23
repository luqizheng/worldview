using Avalonia.Controls;
using WorldView.Desktop.ViewModels;

namespace WorldView.Desktop.Views;

public partial class CreateMapDialog : Window
{
    public CreateMapDialog()
    {
        InitializeComponent();

        // VM 请求关闭 → 关闭窗口（参数 = 是否创建成功；父窗口据此决定是否刷新列表）。
        PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != nameof(DataContext))
            {
                return;
            }

            if (e.OldValue is CreateMapDialogViewModel old)
            {
                old.RequestClose -= OnRequestClose;
            }

            if (e.NewValue is CreateMapDialogViewModel fresh)
            {
                fresh.RequestClose += OnRequestClose;
            }
        };

        // 打开时聚焦名称输入。
        Opened += (_, _) => NameBox.Focus();
    }

    private void OnRequestClose(bool created) => Close(created);
}
