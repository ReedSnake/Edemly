#nullable enable

using System.Windows.Controls.Primitives;

namespace Edemly.Client.Presentation.Pages.Calendar.Helpers
{
    internal static class PageCalendarPopupFactory
    {
        internal static Popup CreateDayTasksPopup()
        {
            return new Popup
            {
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                StaysOpen = false
            };
        }
    }
}
