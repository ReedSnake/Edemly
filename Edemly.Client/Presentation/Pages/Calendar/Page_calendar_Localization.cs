#nullable disable

using Edemly.Client.Application.Services;
using Edemly.Client.Presentation.Common;
using Edemly.Client.Presentation.Pages.Calendar.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Edemly.Client.Presentation.Pages.Calendar
{
    public partial class Page_calendar
    {
        protected override void ApplyTheme()
        {
            if (Content is Grid grid)
            {
                grid.SetResourceReference(Panel.BackgroundProperty, "PageBackgroundBrush");

                foreach (var rectangle in grid.Children.OfType<Rectangle>().Where(r => r.Name != "OverlayBackground"))
                    rectangle.SetResourceReference(Shape.FillProperty, "ThemeSurfaceBrush");
            }

            OverlayBackground?.SetResourceReference(Shape.FillProperty, "ThemeOverlayBrush");

            UpdateFilterButtonsStyle(_currentFilter);

            _ = UpdateCalendarAsync();
            UpdateTasksList(_selectedDateForTask);

            System.Diagnostics.Debug.WriteLine($"[PAGE_CALENDAR] Theme applied: {ThemeService.Instance.CurrentTheme}");
        }

        private void ApplyLocalization()
        {
            var backButton = Get<Button>("BackButton");
            if (backButton != null) backButton.Content = PageNavigationGlyphs.Back;
            if (PrevMonthBtn != null) PrevMonthBtn.Content = PageNavigationGlyphs.Previous;
            if (TodayBtn != null) TodayBtn.Content = DefaultLanguage.TodayButton;
            if (NextMonthBtn != null) NextMonthBtn.Content = PageNavigationGlyphs.Next;
            if (CloseAddPanelBtn != null) CloseAddPanelBtn.Content = PageNavigationGlyphs.Close;
            if (CloseEditPanelBtn != null) CloseEditPanelBtn.Content = PageNavigationGlyphs.Close;

            if (TasksHeader != null) TasksHeader.Text = DefaultLanguage.TodaysTasks;

            var showFilterLabel = Get<TextBlock>("ShowFilterLabel");
            if (showFilterLabel != null) showFilterLabel.Text = DefaultLanguage.ShowLabel;

            if (FilterAllBtn != null) { FilterAllBtn.Content = DefaultLanguage.FilterAll; FilterAllBtn.Tag = DefaultLanguage.FilterAll; }
            if (FilterActiveBtn != null) { FilterActiveBtn.Content = DefaultLanguage.FilterActive; FilterActiveBtn.Tag = DefaultLanguage.FilterActive; }
            if (FilterCompletedBtn != null) { FilterCompletedBtn.Content = DefaultLanguage.FilterCompleted; FilterCompletedBtn.Tag = DefaultLanguage.FilterCompleted; }
            if (FilterUpcomingBtn != null) { FilterUpcomingBtn.Content = DefaultLanguage.FilterUpcoming; FilterUpcomingBtn.Tag = DefaultLanguage.FilterUpcoming; }

            var daySunText = Get<TextBlock>("DaySunText");
            var dayMonText = Get<TextBlock>("DayMonText");
            var dayTueText = Get<TextBlock>("DayTueText");
            var dayWedText = Get<TextBlock>("DayWedText");
            var dayThuText = Get<TextBlock>("DayThuText");
            var dayFriText = Get<TextBlock>("DayFriText");
            var daySatText = Get<TextBlock>("DaySatText");

            if (daySunText != null) daySunText.Text = DefaultLanguage.DaySun;
            if (dayMonText != null) dayMonText.Text = DefaultLanguage.DayMon;
            if (dayTueText != null) dayTueText.Text = DefaultLanguage.DayTue;
            if (dayWedText != null) dayWedText.Text = DefaultLanguage.DayWed;
            if (dayThuText != null) dayThuText.Text = DefaultLanguage.DayThu;
            if (dayFriText != null) dayFriText.Text = DefaultLanguage.DayFri;
            if (daySatText != null) daySatText.Text = DefaultLanguage.DaySat;

            var addTaskPanelTitle = Get<TextBlock>("AddTaskPanelTitle");
            var addTaskNameLabel = Get<Label>("AddTaskNameLabel");
            var addTaskDescLabel = Get<Label>("AddTaskDescLabel");
            var addTaskTimeLabel = Get<Label>("AddTaskTimeLabel");
            var addTaskCategoryLabel = Get<Label>("AddTaskCategoryLabel");
            var cancelAddTaskBtn = Get<Button>("CancelAddTaskBtn");
            var saveAddTaskBtn = Get<Button>("SaveAddTaskBtn");

            if (addTaskPanelTitle != null) addTaskPanelTitle.Text = DefaultLanguage.NewTaskTitle;
            if (addTaskNameLabel != null) addTaskNameLabel.Content = DefaultLanguage.TaskNameLabel;
            if (addTaskDescLabel != null) addTaskDescLabel.Content = DefaultLanguage.TaskDescriptionLabel;
            if (addTaskTimeLabel != null) addTaskTimeLabel.Content = DefaultLanguage.TimeOptionalLabel;
            if (AddTaskHasTime != null) AddTaskHasTime.Content = DefaultLanguage.SetTimeCheckbox;
            if (addTaskCategoryLabel != null) addTaskCategoryLabel.Content = DefaultLanguage.CategoryColorLabel;
            if (cancelAddTaskBtn != null) cancelAddTaskBtn.Content = DefaultLanguage.CancelTaskButton;
            if (saveAddTaskBtn != null) saveAddTaskBtn.Content = DefaultLanguage.SaveTaskButton;
            if (AddTaskTime != null) AddTaskTime.ToolTip = DefaultLanguage.TimeTooltip;

            var editTaskPanelTitle = Get<TextBlock>("EditTaskPanelTitle");
            var editTaskNameLabel = Get<Label>("EditTaskNameLabel");
            var editTaskDescLabel = Get<Label>("EditTaskDescLabel");
            var editTaskTimeLabel = Get<Label>("EditTaskTimeLabel");
            var editTaskCategoryLabel = Get<Label>("EditTaskCategoryLabel");
            var cancelEditTaskBtn = Get<Button>("CancelEditTaskBtn");
            var saveEditTaskBtn = Get<Button>("SaveEditTaskBtn");

            if (editTaskPanelTitle != null) editTaskPanelTitle.Text = DefaultLanguage.EditTaskPanelTitle;
            if (editTaskNameLabel != null) editTaskNameLabel.Content = DefaultLanguage.TaskNameLabel;
            if (editTaskDescLabel != null) editTaskDescLabel.Content = DefaultLanguage.TaskDescriptionLabel;
            if (editTaskTimeLabel != null) editTaskTimeLabel.Content = DefaultLanguage.TimeOptionalLabel;
            if (EditTaskHasTime != null) EditTaskHasTime.Content = DefaultLanguage.SetTimeCheckbox;
            if (editTaskCategoryLabel != null) editTaskCategoryLabel.Content = DefaultLanguage.CategoryColorLabel;
            if (cancelEditTaskBtn != null) cancelEditTaskBtn.Content = DefaultLanguage.CancelTaskButton;
            if (saveEditTaskBtn != null) saveEditTaskBtn.Content = DefaultLanguage.SaveTaskButton;
            if (EditTaskTime != null) EditTaskTime.ToolTip = DefaultLanguage.TimeTooltip;

            var categoryImportantText = Get<TextBlock>("CategoryImportantText");
            var categoryImportantDescText = Get<TextBlock>("CategoryImportantDescText");
            var categoryWorkText = Get<TextBlock>("CategoryWorkText");
            var categoryWorkDescText = Get<TextBlock>("CategoryWorkDescText");
            var categoryPersonalText = Get<TextBlock>("CategoryPersonalText");
            var categoryPersonalDescText = Get<TextBlock>("CategoryPersonalDescText");
            var categorySportsText = Get<TextBlock>("CategorySportsText");
            var categorySportsDescText = Get<TextBlock>("CategorySportsDescText");
            var categoryStudyText = Get<TextBlock>("CategoryStudyText");
            var categoryStudyDescText = Get<TextBlock>("CategoryStudyDescText");
            var categoryEntertainmentText = Get<TextBlock>("CategoryEntertainmentText");
            var categoryEntertainmentDescText = Get<TextBlock>("CategoryEntertainmentDescText");

            if (categoryImportantText != null) categoryImportantText.Text = DefaultLanguage.CategoryImportant;
            if (categoryImportantDescText != null) categoryImportantDescText.Text = DefaultLanguage.CategoryImportantDesc;
            if (categoryWorkText != null) categoryWorkText.Text = DefaultLanguage.CategoryWork;
            if (categoryWorkDescText != null) categoryWorkDescText.Text = DefaultLanguage.CategoryWorkDesc;
            if (categoryPersonalText != null) categoryPersonalText.Text = DefaultLanguage.CategoryPersonal;
            if (categoryPersonalDescText != null) categoryPersonalDescText.Text = DefaultLanguage.CategoryPersonalDesc;
            if (categorySportsText != null) categorySportsText.Text = DefaultLanguage.CategorySports;
            if (categorySportsDescText != null) categorySportsDescText.Text = DefaultLanguage.CategorySportsDesc;
            if (categoryStudyText != null) categoryStudyText.Text = DefaultLanguage.CategoryStudy;
            if (categoryStudyDescText != null) categoryStudyDescText.Text = DefaultLanguage.CategoryStudyDesc;
            if (categoryEntertainmentText != null) categoryEntertainmentText.Text = DefaultLanguage.CategoryEntertainment;
            if (categoryEntertainmentDescText != null) categoryEntertainmentDescText.Text = DefaultLanguage.CategoryEntertainmentDesc;

            var editCategoryImportantText = Get<TextBlock>("EditCategoryImportantText");
            var editCategoryImportantDescText = Get<TextBlock>("EditCategoryImportantDescText");
            var editCategoryWorkText = Get<TextBlock>("EditCategoryWorkText");
            var editCategoryWorkDescText = Get<TextBlock>("EditCategoryWorkDescText");
            var editCategoryPersonalText = Get<TextBlock>("EditCategoryPersonalText");
            var editCategoryPersonalDescText = Get<TextBlock>("EditCategoryPersonalDescText");
            var editCategorySportsText = Get<TextBlock>("EditCategorySportsText");
            var editCategorySportsDescText = Get<TextBlock>("EditCategorySportsDescText");
            var editCategoryStudyText = Get<TextBlock>("EditCategoryStudyText");
            var editCategoryStudyDescText = Get<TextBlock>("EditCategoryStudyDescText");
            var editCategoryEntertainmentText = Get<TextBlock>("EditCategoryEntertainmentText");
            var editCategoryEntertainmentDescText = Get<TextBlock>("EditCategoryEntertainmentDescText");

            if (editCategoryImportantText != null) editCategoryImportantText.Text = DefaultLanguage.CategoryImportant;
            if (editCategoryImportantDescText != null) editCategoryImportantDescText.Text = DefaultLanguage.CategoryImportantDesc;
            if (editCategoryWorkText != null) editCategoryWorkText.Text = DefaultLanguage.CategoryWork;
            if (editCategoryWorkDescText != null) editCategoryWorkDescText.Text = DefaultLanguage.CategoryWorkDesc;
            if (editCategoryPersonalText != null) editCategoryPersonalText.Text = DefaultLanguage.CategoryPersonal;
            if (editCategoryPersonalDescText != null) editCategoryPersonalDescText.Text = DefaultLanguage.CategoryPersonalDesc;
            if (editCategorySportsText != null) editCategorySportsText.Text = DefaultLanguage.CategorySports;
            if (editCategorySportsDescText != null) editCategorySportsDescText.Text = DefaultLanguage.CategorySportsDesc;
            if (editCategoryStudyText != null) editCategoryStudyText.Text = DefaultLanguage.CategoryStudy;
            if (editCategoryStudyDescText != null) editCategoryStudyDescText.Text = DefaultLanguage.CategoryStudyDesc;
            if (editCategoryEntertainmentText != null) editCategoryEntertainmentText.Text = DefaultLanguage.CategoryEntertainment;
            if (editCategoryEntertainmentDescText != null) editCategoryEntertainmentDescText.Text = DefaultLanguage.CategoryEntertainmentDesc;
        }

        private string GetLocalizedCategoryName(int type)
        {
            return PageCalendarCategoryCatalog.GetLocalizedName(type);
        }

        private int GetCategoryTypeByName(string categoryName)
        {
            return PageCalendarCategoryCatalog.GetTypeByName(categoryName);
        }

        private static string GetCategoryColor(int type)
        {
            return PageCalendarCategoryCatalog.GetColor(type);
        }

        private void UpdateFilterButtonsStyle(string activeFilter)
        {
            var allButtons = new[]
            {
                Get<Button>("FilterAllBtn"),
                Get<Button>("FilterActiveBtn"),
                Get<Button>("FilterCompletedBtn"),
                Get<Button>("FilterUpcomingBtn")
            };

            foreach (var btn in allButtons)
            {
                if (btn == null) continue;

                string btnFilter = btn.Tag as string ?? DefaultLanguage.FilterAll;

                if (btnFilter == activeFilter)
                {
                    SetThemeBrush(btn, Control.BackgroundProperty, "ThemeSecondaryBrush");
                    SetThemeBrush(btn, Control.ForegroundProperty, "ThemeOnSecondaryTextBrush");
                    btn.BorderBrush = Brushes.Transparent;
                    btn.FontWeight = FontWeights.Bold;
                }
                else
                {
                    SetThemeBrush(btn, Control.BackgroundProperty, "ThemeSurfaceBrush");
                    SetThemeBrush(btn, Control.ForegroundProperty, "ThemeTextPrimaryBrush");
                    SetThemeBrush(btn, Control.BorderBrushProperty, "ThemeBorderLightBrush");
                    btn.BorderThickness = new Thickness(1);
                    btn.FontWeight = FontWeights.SemiBold;
                }
            }
        }
    }
}
