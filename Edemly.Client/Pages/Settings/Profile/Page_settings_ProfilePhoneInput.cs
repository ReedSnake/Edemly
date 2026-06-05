#nullable enable

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace Edemly.Client.Pages.Settings
{
    public partial class Page_settings
    {
        private static readonly Regex PhoneInputRegex = new(@"^[0-9+\-\s()]+$");

        private void PhoneNumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !PhoneInputRegex.IsMatch(e.Text);
        }

        private void PhoneNumberTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = false;
            }
        }

        private void PhoneNumberTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string;
            if (string.IsNullOrWhiteSpace(text) || !PhoneInputRegex.IsMatch(text))
            {
                e.CancelCommand();
            }
        }
    }
}
