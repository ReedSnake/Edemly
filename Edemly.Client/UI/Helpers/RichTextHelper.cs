#nullable disable
using Edemly;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Edemly.Client.UI.Helpers
{
    /// <summary>
    /// Допоміжний клас для створення TextBlock з підтримкою посилань, email, телефонів
    /// </summary>
    public static class RichTextHelper
    {
        private static readonly Regex UrlRegex = new Regex(
            @"(https?://[^\s]+)|" +
            @"(www\.[^\s]+)|" +
            @"([a-zA-Z0-9-]+\.(com|org|net|edu|gov|uk|us|info|biz|io|co|me|tv|app|dev)[^\s]*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex EmailRegex = new Regex(
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            RegexOptions.Compiled);

        private static readonly Regex PhoneRegex = new Regex(
            @"(\+?\d{1,4}[\s-]?)?\(?\d{1,4}\)?[\s-]?\d{1,4}[\s-]?\d{1,4}[\s-]?\d{0,9}",
            RegexOptions.Compiled);

        /// <summary>
        /// Створює TextBlock з автоматичним виявленням посилань, email, телефонів
        /// </summary>
        public static TextBlock CreateRichTextBlock(string text, Brush foregroundBrush, bool allowSelection = true)
        {
            var textBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = foregroundBrush,
                FontSize = 14
            };

            if (allowSelection)
            {
                textBlock.Cursor = Cursors.IBeam;
                
                textBlock.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ClickCount == 2)
                    {
                        SelectWord(textBlock, e.GetPosition(textBlock));
                    }
                };

                textBlock.MouseMove += (s, e) =>
                {
                    if (e.LeftButton == MouseButtonState.Pressed)
                    {
                        textBlock.Focus();
                    }
                };

                textBlock.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        CopySelectedText(textBlock);
                        e.Handled = true;
                    }
                };
            }

            ParseAndAddInlines(text, textBlock, foregroundBrush);

            return textBlock;
        }

        private static void ParseAndAddInlines(string text, TextBlock textBlock, Brush foregroundBrush)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                textBlock.Inlines.Add(new Run(text));
                return;
            }

            int lastIndex = 0;
            var matches = new System.Collections.Generic.List<(int Start, int Length, string Type, string Value)>();

            foreach (Match match in UrlRegex.Matches(text))
            {
                matches.Add((match.Index, match.Length, "url", match.Value));
            }

            foreach (Match match in EmailRegex.Matches(text))
            {
                bool overlaps = matches.Any(m => 
                    match.Index >= m.Start && match.Index < m.Start + m.Length);
                
                if (!overlaps)
                {
                    matches.Add((match.Index, match.Length, "email", match.Value));
                }
            }

            foreach (Match match in PhoneRegex.Matches(text))
            {
                var digitsOnly = Regex.Replace(match.Value, @"\D", "");
                if (digitsOnly.Length >= 9)
                {
                    bool overlaps = matches.Any(m => 
                        match.Index >= m.Start && match.Index < m.Start + m.Length);
                    
                    if (!overlaps)
                    {
                        matches.Add((match.Index, match.Length, "phone", match.Value));
                    }
                }
            }

            matches = matches.OrderBy(m => m.Start).ToList();

            foreach (var match in matches)
            {
                if (match.Start > lastIndex)
                {
                    var beforeText = text.Substring(lastIndex, match.Start - lastIndex);
                    textBlock.Inlines.Add(new Run(beforeText));
                }

                var hyperlink = CreateHyperlink(match.Value, match.Type);
                textBlock.Inlines.Add(hyperlink);

                lastIndex = match.Start + match.Length;
            }

            if (lastIndex < text.Length)
            {
                textBlock.Inlines.Add(new Run(text.Substring(lastIndex)));
            }
        }

        private static Hyperlink CreateHyperlink(string value, string type)
        {
            var hyperlink = new Hyperlink(new Run(value))
            {
                Foreground = new SolidColorBrush(Color.FromRgb(100, 181, 246)),
                TextDecorations = null,
                Cursor = Cursors.Hand
            };

            hyperlink.MouseEnter += (s, e) =>
            {
                hyperlink.TextDecorations = TextDecorations.Underline;
            };

            hyperlink.MouseLeave += (s, e) =>
            {
                hyperlink.TextDecorations = null;
            };

            hyperlink.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                HandleHyperlinkClick(value, type);
            };

            hyperlink.MouseRightButtonDown += (s, e) =>
            {
                e.Handled = true;
                Clipboard.SetText(value);
                
                ToolTipService.SetToolTip(hyperlink, "Copied!");
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (sender, args) =>
                {
                    ToolTipService.SetToolTip(hyperlink, null);
                    timer.Stop();
                };
                timer.Start();
            };

            return hyperlink;
        }

        private static void HandleHyperlinkClick(string value, string type)
        {
            try
            {
                switch (type)
                {
                    case "url":
                        string url = value;
                        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                        {
                            url = "http://" + url;
                        }
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                        break;

                    case "email":
                        Clipboard.SetText(value);
                        MessageBox.Show($"Email copied to clipboard:\n{value}", "Email", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        break;

                    case "phone":
                        Clipboard.SetText(value);
                        MessageBox.Show($"Phone number copied to clipboard:\n{value}", "Phone", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling hyperlink: {ex.Message}");
            }
        }

        private static void SelectWord(TextBlock textBlock, Point position)
        {
            var text = GetTextFromTextBlock(textBlock);
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
            }
        }

        private static void CopySelectedText(TextBlock textBlock)
        {
            var text = GetTextFromTextBlock(textBlock);
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
            }
        }

        private static string GetTextFromTextBlock(TextBlock textBlock)
        {
            var text = "";
            foreach (var inline in textBlock.Inlines)
            {
                if (inline is Run run)
                {
                    text += run.Text;
                }
                else if (inline is Hyperlink hyperlink)
                {
                    text += ((Run)hyperlink.Inlines.FirstInline).Text;
                }
            }
            return text;
        }
    }
}
