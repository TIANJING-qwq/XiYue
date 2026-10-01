using Avalonia.Controls;
using Avalonia.Interactivity;
using SBtools.Services;

namespace SBtools.Views;

public partial class ImageViewerWindow : Window
{
    public ImageViewerWindow()
    {
        InitializeComponent();
    }

    public ImageViewerWindow(GalleryItem item) : this()
    {
        if (item != null)
        {
            NameText.Text = item.Name;
            MainImage.Source = item.FullImage;
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}