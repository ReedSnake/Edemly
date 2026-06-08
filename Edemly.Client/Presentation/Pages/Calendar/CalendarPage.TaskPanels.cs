#nullable disable

using Edemly.Client.Presentation.Pages.Calendar.Helpers;
using Edemly.Contracts.Remindings;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace Edemly.Client.Presentation.Pages.Calendar
{
    public partial class CalendarPage
    {
        private void ShowAddTaskPanel(DateTime? date = null)
        {
            if (date.HasValue)
            {
                _selectedDateForTask = date.Value;

                if (IsPastDate(_selectedDateForTask))
                {
                    MessageBox.Show(DefaultLanguage.CannotAddPastDate,
                                  DefaultLanguage.InvalidDate,
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                    return;
                }
            }

            var title = Get<TextBox>("AddTaskTitle");
            var desc = Get<TextBox>("AddTaskDescription");
            var colorBlue = Get<RadioButton>("ColorBlue");
            var hasTimeChk = Get<CheckBox>("AddTaskHasTime");
            var timeBorder = Get<Border>("AddTaskTimeBorder");
            var timeBox = Get<TextBox>("AddTaskTime");
            var panel = Get<Border>("AddTaskPanel");
            var overlay = Get<Rectangle>("OverlayBackground");

            if (title != null) title.Text = string.Empty;
            if (desc != null) desc.Text = string.Empty;
            if (colorBlue != null) colorBlue.IsChecked = true;

            if (hasTimeChk != null) hasTimeChk.IsChecked = false;
            if (timeBorder != null) timeBorder.Visibility = Visibility.Collapsed;
            if (timeBox != null) timeBox.Text = "09:00";

            if (panel != null) panel.Visibility = Visibility.Visible;
            if (overlay != null) overlay.Visibility = Visibility.Visible;

            if (panel != null) panel.Tag = null;

            if (title != null) title.Focus();
        }

        private void HideAddTaskPanel()
        {
            var panel = Get<Border>("AddTaskPanel");
            var overlay = Get<Rectangle>("OverlayBackground");
            if (panel != null) panel.Visibility = Visibility.Collapsed;
            if (overlay != null) overlay.Visibility = Visibility.Collapsed;
            if (panel != null) panel.Tag = null;
        }

        private void HideEditTaskPanel()
        {
            var panel = Get<Border>("EditTaskPanel");
            var overlay = Get<Rectangle>("OverlayBackground");
            if (panel != null) panel.Visibility = Visibility.Collapsed;
            if (overlay != null) overlay.Visibility = Visibility.Collapsed;
            if (panel != null) panel.Tag = null;
        }

        private void OpenEditTaskPanel(RemindingDto task)
        {
            _selectedDateForTask = task.LastTime.Date;

            var title = Get<TextBox>("EditTaskTitle");
            var desc = Get<TextBox>("EditTaskDescription");
            var hasTimeChk = Get<CheckBox>("EditTaskHasTime");
            var timeBorder = Get<Border>("EditTaskTimeBorder");
            var timeBox = Get<TextBox>("EditTaskTime");

            if (title != null) title.Text = task.Name;
            if (desc != null) desc.Text = task.Content;

            if (hasTimeChk != null) hasTimeChk.IsChecked = task.ShowTime;
            if (timeBorder != null) timeBorder.Visibility = task.ShowTime ? Visibility.Visible : Visibility.Collapsed;
            if (timeBox != null) timeBox.Text = task.ShowTime ? task.LastTime.ToString("HH:mm") : "09:00";

            string taskColor = GetCategoryColor(task.Type);

            switch (taskColor)
            {
                case "#FF6B6B": Get<RadioButton>("EditColorRed").IsChecked = true; break;
                case "#4A6CF7": Get<RadioButton>("EditColorBlue").IsChecked = true; break;
                case "#32CD32": Get<RadioButton>("EditColorGreen").IsChecked = true; break;
                case "#FFA500": Get<RadioButton>("EditColorOrange").IsChecked = true; break;
                case "#9B59B6": Get<RadioButton>("EditColorPurple").IsChecked = true; break;
                case "#FF69B4": Get<RadioButton>("EditColorPink").IsChecked = true; break;
                default: Get<RadioButton>("EditColorBlue").IsChecked = true; break;
            }

            var panel = Get<Border>("EditTaskPanel");
            var overlay = Get<Rectangle>("OverlayBackground");
            if (panel != null) panel.Visibility = Visibility.Visible;
            if (overlay != null) overlay.Visibility = Visibility.Visible;

            if (panel != null) panel.Tag = task;
        }

        private async void ConfirmAddTaskBtn_Click(object sender, RoutedEventArgs e)
        {
            var panel = Get<Border>("AddTaskPanel");
            var editingTask = panel?.Tag as CreateRemindingDto;

            var title = Get<TextBox>("AddTaskTitle");
            var desc = Get<TextBox>("AddTaskDescription");

            if (title == null) return;

            if (string.IsNullOrWhiteSpace(title.Text))
            {
                MessageBox.Show(DefaultLanguage.EnterTaskName, DefaultLanguage.WarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                title.Focus();
                return;
            }

            if (IsPastDate(_selectedDateForTask))
            {
                MessageBox.Show(DefaultLanguage.CannotAddPastDate,
                               DefaultLanguage.InvalidDate,
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);
                return;
            }

            bool hasTime = Get<CheckBox>("AddTaskHasTime")?.IsChecked == true;
            int hour = 9, minute = 0;

            if (hasTime)
            {
                var timeBox = Get<TextBox>("AddTaskTime");
                if (timeBox != null)
                {
                    string timeText = timeBox.Text.Trim();
                    if (string.IsNullOrEmpty(timeText))
                    {
                        MessageBox.Show(DefaultLanguage.EnterTimeLabel, DefaultLanguage.WarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                        timeBox.Focus();
                        return;
                    }

                    if (!CalendarPageTimeInputHelper.TryParseTime(timeText, out DateTime parsedTime))
                    {
                        MessageBox.Show(DefaultLanguage.InvalidTimeFormat,
                                       DefaultLanguage.WarningTitle,
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Warning);
                        timeBox.Focus();
                        timeBox.SelectAll();
                        return;
                    }

                    hour = parsedTime.Hour;
                    minute = parsedTime.Minute;
                    timeBox.Text = $"{hour:00}:{minute:00}";
                }
            }

            int selectedType = 1;

            var red = Get<RadioButton>("ColorRed"); if (red?.IsChecked == true) selectedType = 0;
            var blue = Get<RadioButton>("ColorBlue"); if (blue?.IsChecked == true) selectedType = 1;
            var green = Get<RadioButton>("ColorGreen"); if (green?.IsChecked == true) selectedType = 2;
            var orange = Get<RadioButton>("ColorOrange"); if (orange?.IsChecked == true) selectedType = 3;
            var purple = Get<RadioButton>("ColorPurple"); if (purple?.IsChecked == true) selectedType = 4;
            var pink = Get<RadioButton>("ColorPink"); if (pink?.IsChecked == true) selectedType = 5;

            DateTime taskDateTime = new DateTime(_selectedDateForTask.Year, _selectedDateForTask.Month,
                                                 _selectedDateForTask.Day, hour, minute, 0);

            if (editingTask != null)
            {
                editingTask.Name = title.Text.Trim();
                editingTask.Content = desc?.Text.Trim();
                editingTask.LastTime = taskDateTime;
                editingTask.Type = selectedType;
                editingTask.ShowTime = hasTime;
            }
            else
            {
                var newTask = new CreateRemindingDto
                {
                    Name = title.Text.Trim(),
                    Content = desc?.Text.Trim(),
                    LastTime = taskDateTime,
                    Type = selectedType,
                    ShowTime = hasTime,
                };

                try
                {
                    var createdReminding = await _apiClient.CreateRemindingAsync(newTask);
                    if (createdReminding != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TASK] Reminding created with ID {createdReminding.Id}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TASK] Failed to create reminding: {ex.Message}");
                }
            }

            await UpdateCalendarAsync();
            UpdateTasksList(_selectedDateForTask);
            HideAddTaskPanel();
        }

        private async void ConfirmEditTaskBtn_Click(object sender, RoutedEventArgs e)
        {
            var panel = Get<Border>("EditTaskPanel");
            var originalTask = panel?.Tag as RemindingDto;
            if (originalTask == null) return;

            var title = Get<TextBox>("EditTaskTitle");
            var desc = Get<TextBox>("EditTaskDescription");
            var hasTimeChk = Get<CheckBox>("EditTaskHasTime");
            var timeBox = Get<TextBox>("EditTaskTime");

            if (title == null || string.IsNullOrWhiteSpace(title.Text))
            {
                MessageBox.Show(DefaultLanguage.EnterTaskName, DefaultLanguage.WarningTitle,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                title?.Focus();
                return;
            }

            if (IsPastDate(_selectedDateForTask))
            {
                MessageBox.Show(DefaultLanguage.CannotEditPastDate,
                    DefaultLanguage.InvalidDate,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool hasTime = hasTimeChk?.IsChecked == true;
            int hour = 9, minute = 0;

            if (hasTime)
            {
                if (timeBox == null || string.IsNullOrWhiteSpace(timeBox.Text))
                {
                    MessageBox.Show(DefaultLanguage.EnterTimeLabel, DefaultLanguage.WarningTitle,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    timeBox?.Focus();
                    return;
                }

                if (!CalendarPageTimeInputHelper.TryParseTime(timeBox.Text, out DateTime parsed))
                {
                    MessageBox.Show(DefaultLanguage.InvalidTimeFormat, DefaultLanguage.InvalidDate,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    timeBox.Focus();
                    timeBox.SelectAll();
                    return;
                }

                hour = parsed.Hour;
                minute = parsed.Minute;
                timeBox.Text = $"{hour:00}:{minute:00}";
            }

            DateTime finalTime = new DateTime(
                _selectedDateForTask.Year,
                _selectedDateForTask.Month,
                _selectedDateForTask.Day,
                hour,
                minute,
                0);

            int selectedType = 1;
            if (Get<RadioButton>("EditColorRed")?.IsChecked == true) selectedType = 0;
            if (Get<RadioButton>("EditColorBlue")?.IsChecked == true) selectedType = 1;
            if (Get<RadioButton>("EditColorGreen")?.IsChecked == true) selectedType = 2;
            if (Get<RadioButton>("EditColorOrange")?.IsChecked == true) selectedType = 3;
            if (Get<RadioButton>("EditColorPurple")?.IsChecked == true) selectedType = 4;
            if (Get<RadioButton>("EditColorPink")?.IsChecked == true) selectedType = 5;

            var model = new UpdateRemindingDto
            {
                Id = originalTask.Id,
                Name = title.Text.Trim(),
                Content = desc?.Text.Trim(),
                ShowTime = hasTime,
                LastTime = finalTime,
                Type = selectedType,
                ShouldNotify = originalTask.ShouldNotify,
                IsCompleted = originalTask.IsCompleted
            };

            await _apiClient.UpdateRemindingAsync(model);

            await UpdateCalendarAsync();
            UpdateTasksList(_selectedDateForTask);

            HideEditTaskPanel();
        }

        private void CancelAddTaskBtn_Click(object sender, RoutedEventArgs e) => HideAddTaskPanel();

        private void CloseAddPanelBtn_Click(object sender, RoutedEventArgs e) => HideAddTaskPanel();

        private void CancelEditTaskBtn_Click(object sender, RoutedEventArgs e) => HideEditTaskPanel();

        private void CloseEditPanelBtn_Click(object sender, RoutedEventArgs e) => HideEditTaskPanel();

        private void AddTaskBtn_Click(object sender, RoutedEventArgs e)
        {
            if (IsPastDate(_selectedDateForTask))
            {
                MessageBox.Show(DefaultLanguage.CannotAddPastDate,
                              DefaultLanguage.InvalidDate,
                              MessageBoxButton.OK,
                              MessageBoxImage.Warning);
                return;
            }

            ShowAddTaskPanel(_selectedDateForTask);
        }
    }
}
