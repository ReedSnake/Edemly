#nullable enable

namespace Edemly.Client.Presentation.Pages.Calendar.Helpers
{
    internal static class CalendarPageCategoryCatalog
    {
        private static readonly Dictionary<int, string> Colors = new()
        {
            { 0, "#FF6B6B" },
            { 1, "#4A6CF7" },
            { 2, "#32CD32" },
            { 3, "#FFA500" },
            { 4, "#9B59B6" },
            { 5, "#FF69B4" }
        };

        internal static string GetLocalizedName(int type)
        {
            return type switch
            {
                0 => DefaultLanguage.CategoryImportant,
                1 => DefaultLanguage.CategoryWork,
                2 => DefaultLanguage.CategoryPersonal,
                3 => DefaultLanguage.CategorySports,
                4 => DefaultLanguage.CategoryStudy,
                5 => DefaultLanguage.CategoryEntertainment,
                _ => DefaultLanguage.CategoryWork
            };
        }

        internal static int GetTypeByName(string categoryName)
        {
            if (categoryName == DefaultLanguage.CategoryImportant) return 0;
            if (categoryName == DefaultLanguage.CategoryWork) return 1;
            if (categoryName == DefaultLanguage.CategoryPersonal) return 2;
            if (categoryName == DefaultLanguage.CategorySports) return 3;
            if (categoryName == DefaultLanguage.CategoryStudy) return 4;
            if (categoryName == DefaultLanguage.CategoryEntertainment) return 5;
            return 1;
        }

        internal static string GetColor(int type)
        {
            return Colors.TryGetValue(type, out var color)
                ? color
                : Colors[1];
        }
    }
}
