<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:SBtools.ViewModels"
             x:Class="SBtools.Views.AboutTabView">
    <Design.DataContext>
        <vm:AboutTabViewModel />
    </Design.DataContext>

    <StackPanel Margin="20" VerticalAlignment="Center" HorizontalAlignment="Center">
        <TextBlock Text="{Binding Title}" FontSize="18" FontWeight="Bold" Foreground="#007aff" />
        <TextBlock Text="{Binding Author}" FontSize="14" Margin="10,0,0,0" />
        <TextBlock Text="{Binding BuildInfo}" FontSize="14" />
        <TextBlock Text="{Binding Version}" FontSize="12" Foreground="#8e8e93" />
        <TextBlock Text="{Binding ProjectName}" FontSize="14" />
    </StackPanel>
</UserControl>