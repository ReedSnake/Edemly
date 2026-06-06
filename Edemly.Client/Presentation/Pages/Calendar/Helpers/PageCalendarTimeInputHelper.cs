#nullable enable

using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;

namespace Edemly.Client.Presentation.Pages.Calendar.Helpers
{
    internal static class PageCalendarTimeInputHelper
    {
        internal static bool HandleTextInput(TextBox? textBox, string input)
        {
            if (textBox == null || string.IsNullOrEmpty(input))
            {
                return false;
            }

            var currentText = textBox.Text;
            var caretIndex = textBox.CaretIndex;

            if (!char.IsDigit(input[0]) && input != ":")
            {
                return true;
            }

            if (caretIndex == 2 && input != ":")
            {
                textBox.Text = currentText.Insert(2, ":");
                textBox.CaretIndex = 3;

                var newText = textBox.Text.Insert(3, input);
                if (newText.Length > 5)
                {
                    newText = newText[..5];
                }

                textBox.Text = newText;
                textBox.CaretIndex = 4;
                return true;
            }

            if (caretIndex == 2 && input == ":")
            {
                textBox.CaretIndex = 3;
                return true;
            }

            return false;
        }

        internal static bool HandlePreviewKeyDown(TextBox? textBox, Key key)
        {
            if (textBox == null)
            {
                return false;
            }

            if (key == Key.Back)
            {
                if (textBox.CaretIndex == 3 && textBox.Text.Length > 3 && textBox.Text[2] == ':')
                {
                    textBox.Text = textBox.Text.Remove(3, 1);
                    textBox.CaretIndex = 3;
                    return true;
                }

                if (textBox.CaretIndex == 2 && textBox.Text.Length > 2 && textBox.Text[2] == ':')
                {
                    textBox.Text = textBox.Text.Remove(2, 1);
                    textBox.CaretIndex = 2;
                    return true;
                }
            }
            else if (key == Key.Up || key == Key.Down)
            {
                var parts = textBox.Text.Split(':');
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0], out var hours) ||
                    !int.TryParse(parts[1], out var minutes))
                {
                    return true;
                }

                var editingHours = textBox.CaretIndex <= 2;

                if (editingHours)
                {
                    hours = key == Key.Up
                        ? (hours + 1) % 24
                        : (hours - 1 + 24) % 24;
                }
                else
                {
                    minutes = key == Key.Up
                        ? (minutes + 1) % 60
                        : (minutes - 1 + 60) % 60;
                }

                textBox.Text = $"{hours:00}:{minutes:00}";
                textBox.CaretIndex = editingHours ? 2 : 5;
                return true;
            }
            else if (key == Key.Tab && textBox.CaretIndex <= 2)
            {
                textBox.CaretIndex = 3;
                return true;
            }

            return false;
        }

        internal static bool TryParseTime(string? text, out DateTime parsedTime)
        {
            return DateTime.TryParseExact(text?.Trim(),
                    "HH:mm",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out parsedTime)
                || DateTime.TryParseExact(text?.Trim(),
                    "H:mm",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out parsedTime);
        }
    }
}
