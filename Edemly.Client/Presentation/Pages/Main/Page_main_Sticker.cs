using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class Page_main
    {
        public ObservableCollection<StickerModel> Stickers { get; set; } = new ObservableCollection<StickerModel>();

        private void LoadStickers()
        {
            try
            {
                Stickers.Clear();

                string stickersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Stickers");

                if (!Directory.Exists(stickersPath))
                {
                    Directory.CreateDirectory(stickersPath);
                    System.Diagnostics.Debug.WriteLine($"[STICKERS] Created directory: {stickersPath}");
                    return;
                }

                var supported = new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" };

                var files = Directory.EnumerateFiles(stickersPath, "*.*", SearchOption.TopDirectoryOnly)
                                     .Where(f => supported.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                                     .OrderBy(f => f)
                                     .ToList();

                System.Diagnostics.Debug.WriteLine($"[STICKERS] Found {files.Count} files in {stickersPath}");

                foreach (var file in files)
                {
                    try
                    {
                        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = fs;
                            bitmap.DecodePixelWidth = 128;
                            bitmap.EndInit();
                            bitmap.Freeze();

                            Stickers.Add(new StickerModel(file, bitmap));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[STICKERS] Failed to load {file}: {ex.Message}");
                    }
                }

                try
                {
                    if (StickersGrid != null)
                    {
                        StickersGrid.Children.Clear();

                        foreach (var s in Stickers)
                        {
                            var img = new Image
                            {
                                Source = s.ImageSource,
                                Width = 64,
                                Height = 64,
                                Stretch = Stretch.Uniform
                            };

                            var btn = new Button
                            {
                                Width = 78,
                                Height = 78,
                                Margin = new Thickness(6),
                                Background = System.Windows.Media.Brushes.Transparent,
                                BorderThickness = new Thickness(0),
                                Tag = s.FilePath,
                                Content = img,
                                ToolTip = System.IO.Path.GetFileName(s.FilePath)
                            };

                            btn.Click += Sticker_Click;

                            StickersGrid.Children.Add(btn);
                        }

                        System.Diagnostics.Debug.WriteLine($"[STICKERS UI] Added {StickersGrid.Children.Count} children to StickersGrid.");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[STICKERS UI] StickersGrid is null (check XAML name).");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[STICKERS UI] Error populating grid: {ex.Message}");
                }

                System.Diagnostics.Debug.WriteLine($"[STICKERS] Loaded {Stickers.Count} stickers.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[STICKERS] Error loading stickers: {ex.Message}");
            }
        }

        private void ToggleStickers_Click(object sender, RoutedEventArgs e)
        {
            if (StickersPanel.Visibility == Visibility.Visible)
            {
                StickersPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                StickersPanel.Visibility = Visibility.Visible;
            }
        }
        public class StickerModel
        {
            public StickerModel(string filePath, ImageSource imageSource)
            {
                FilePath = filePath;
                ImageSource = imageSource;
            }

            public string FilePath { get; }
            public ImageSource ImageSource { get; }
        }
        private void StickerButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleStickers_Click(sender, e);
        }

        private void CloseStickers_Click(object sender, RoutedEventArgs e)
        {
            StickersPanel.Visibility = Visibility.Collapsed;
        }

        private async void Sticker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filePath)
            {
                System.Diagnostics.Debug.WriteLine($"[STICKER CLICK] Selected: {filePath}");

                try { btn.Opacity = 0.5; await Task.Delay(100); btn.Opacity = 1.0; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[STICKER CLICK] UI animation failed: {ex}"); }

                try
                {
                    if (File.Exists(filePath))
                    {
                        await SendFileAsync(filePath, string.Empty);
                    }

                    StickersPanel.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[STICKER SEND ERROR] {ex.Message}");
                }
            }
        }
    }
}
