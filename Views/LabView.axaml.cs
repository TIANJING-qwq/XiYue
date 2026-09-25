using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class LabView : UserControl
{
    public LabView()
    {
        InitializeComponent();
    }

    private void TestNotification_Click(object? sender, RoutedEventArgs e)
    {
        MainWindow.PushToast(
            "测试通知",
            "这是一条带亚克力背景、非线性动画和 5 秒倒计时进度条的通知。");
    }

    private async void BurstNotification_Click(object? sender, RoutedEventArgs e)
    {
        for (int i = 1; i <= 5; i++)
        {
            MainWindow.PushToast(
                $"通知 #{i}",
                $"这是第 {i} 条通知，用于测试向下堆叠与超出压缩效果。");
            await Task.Delay(180);
        }
    }

    private void CustomNotification_Click(object? sender, RoutedEventArgs e)
    {
        CustomPanel.IsVisible = !CustomPanel.IsVisible;
    }

    private void PushCustom_Click(object? sender, RoutedEventArgs e)
    {
        var title = string.IsNullOrWhiteSpace(CustomTitle.Text)
            ? "通知"
            : CustomTitle.Text!.Trim();

        var message = string.IsNullOrWhiteSpace(CustomMessage.Text)
            ? "（无内容）"
            : CustomMessage.Text!.Trim();

        MainWindow.PushToast(title, message);

        // 清空并收起
        CustomTitle.Text = "";
        CustomMessage.Text = "";
        CustomPanel.IsVisible = false;
    }

    private void CancelCustom_Click(object? sender, RoutedEventArgs e)
    {
        CustomTitle.Text = "";
        CustomMessage.Text = "";
        CustomPanel.IsVisible = false;
    }
}