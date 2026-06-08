using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Edemly.Client.Presentation.Pages.Main.Helpers;

public static class MainPageInputHelper
{
    public static bool IsPlaceholderText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;

        text = text.Trim();

        return text == DefaultLanguage.TypeMessage
            || text == DefaultLanguage.Loading
            || text == "Message..."
            || text == "Type a message...";
    }
}
