#nullable disable

using Edemly.Client.Presentation.Common;
using Edemly.Client.Presentation.Pages.Calendar.Helpers;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Edemly.Client.Api;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Edemly.Client.Api.Remindings;
namespace Edemly.Client.Presentation.Pages.Calendar
{
    public partial class CalendarPage : ThemedPage
    {
        private Popup _dayTasksPopup;

        private DateTime _currentDate;
        private List<RemindingDto> _tasks = new List<RemindingDto>();
        private DateTime _selectedDateForTask;
        private string _currentFilter = string.Empty;
        private readonly IRemindingApiClient _apiClient;

        public CalendarPage()
        {
            InitializeComponent();

            _dayTasksPopup = CalendarPagePopupFactory.CreateDayTasksPopup();

            try
            {
                var sv = Get<ScrollViewer>("CategoryScrollViewer");
                if (sv != null)
                {
                    sv.PreviewMouseWheel += CategoryScrollViewer_PreviewMouseWheel;
                }
            }
            catch { }

            _currentDate = DateTime.Today;
            _selectedDateForTask = DateTime.Today;
            _apiClient = App.ApiClients.Remindings;

            ApplyLocalization();

            _currentFilter = DefaultLanguage.FilterAll;

            _ = UpdateCalendarAsync();
            UpdateTasksList();
            UpdateFilterButtonsStyle(_currentFilter);
        }

        private Style GetStyle(string resourceKey)
        {
            return TryFindResource(resourceKey) as Style;
        }

        private void SetThemeBrush(FrameworkElement element, DependencyProperty property, string resourceKey)
        {
            element?.SetResourceReference(property, resourceKey);
        }

        private static string GetTaskTextResourceKey(RemindingDto task)
        {
            return task.IsCompleted ? "ThemeDisabledTextBrush" : "ThemeTextPrimaryBrush";
        }

        private static Brush CreateColorBrush(string color)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }

        private T Get<T>(string name) where T : class
        {
            return this.FindName(name) as T;
        }

        private bool IsPastDate(DateTime date)
        {
            return date.Date < DateTime.Today.Date;
        }

        private async Task UpdateCalendarAsync()
        {
            var calendarGrid = Get<UniformGrid>("CalendarGrid");
            var monthText = Get<TextBlock>("MonthText");
            var yearText = Get<TextBlock>("YearText");

            if (calendarGrid == null || monthText == null || yearText == null)
                return;

            calendarGrid.Children.Clear();

            monthText.Text = GetLocalizedMonthName(_currentDate.Month);
            yearText.Text = _currentDate.Year.ToString();

            DateTime firstDayOfMonth = new DateTime(_currentDate.Year, _currentDate.Month, 1);
            int startDay = (int)firstDayOfMonth.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(_currentDate.Year, _currentDate.Month);

            try
            {
                _tasks = await _apiClient.GetMyRemindingsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CALENDAR] Failed to load remindings: {ex.Message}");
            }

            for (int i = 0; i < 42; i++)
            {
                Border dayBorder = new Border
                {
                    Margin = new Thickness(3),
                    CornerRadius = new CornerRadius(12),
                    Background = Brushes.Transparent
                };

                Button dayButton = new Button
                {
                    Style = GetStyle("DayButtonStyle"),
                    Content = "",
                    Height = 60,
                    Tag = null,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch
                };

                TextBlock dayText = new TextBlock
                {
                    FontSize = 16,
                    FontWeight = FontWeights.Normal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 3, 3, 0)
                };
                SetThemeBrush(dayText, TextBlock.ForegroundProperty, "ThemeTextPrimaryBrush");

                StackPanel taskIndicator = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0)
                };

                Grid dayGrid = new Grid();
                dayGrid.Children.Add(dayText);
                dayGrid.Children.Add(taskIndicator);

                dayButton.Content = dayGrid;

                if (i >= startDay && i < startDay + daysInMonth)
                {
                    int dayNumber = i - startDay + 1;
                    DateTime cellDate = new DateTime(_currentDate.Year, _currentDate.Month, dayNumber);

                    dayButton.Tag = cellDate;
                    dayText.Text = dayNumber.ToString();

                    var tasksForDay = _tasks.Where(t => t.LastTime.Date == cellDate.Date)
                                             .OrderBy(t => t.ShowTime ? t.LastTime.TimeOfDay : TimeSpan.Zero)
                                             .ToList();

                    foreach (var task in tasksForDay.Take(3))
                    {
                        string taskColor = GetCategoryColor(task.Type);

                        Ellipse dot = new Ellipse
                        {
                            Width = 6,
                            Height = 6,
                            Margin = new Thickness(3, 0, 3, 0),
                            Fill = CreateColorBrush(taskColor)
                        };
                        taskIndicator.Children.Add(dot);
                    }

                    bool isPastDate = IsPastDate(cellDate);
                    bool isToday = cellDate.Date == DateTime.Today.Date;

                    if (isToday)
                    {
                        SetThemeBrush(dayButton, Control.BackgroundProperty, "ThemeSecondaryBrush");
                        SetThemeBrush(dayButton, Control.ForegroundProperty, "ThemeOnSecondaryTextBrush");
                        SetThemeBrush(dayText, TextBlock.ForegroundProperty, "ThemeOnSecondaryTextBrush");
                        dayButton.IsEnabled = true;
                        dayButton.Cursor = System.Windows.Input.Cursors.Hand;
                        dayButton.ToolTip = $"{DefaultLanguage.Today} - {DefaultLanguage.AddTaskButton}/{DefaultLanguage.NoTasks}";
                    }
                    else if (isPastDate)
                    {
                        SetThemeBrush(dayButton, Control.BackgroundProperty, "ThemeBorderLightBrush");
                        SetThemeBrush(dayButton, Control.ForegroundProperty, "ThemeDisabledTextBrush");
                        SetThemeBrush(dayText, TextBlock.ForegroundProperty, "ThemeDisabledTextBrush");
                        dayButton.Opacity = 0.7;
                        dayButton.IsEnabled = true;
                        dayButton.Cursor = System.Windows.Input.Cursors.Hand;
                        dayButton.ToolTip = $"{DefaultLanguage.NoTasks} ({DefaultLanguage.CannotAddPastDate})";
                        foreach (Ellipse dot in taskIndicator.Children)
                            SetThemeBrush(dot, Shape.FillProperty, "ThemeDisabledTextBrush");
                    }
                    else
                    {
                        SetThemeBrush(dayButton, Control.BackgroundProperty, "ThemeSurfaceBrush");
                        SetThemeBrush(dayButton, Control.ForegroundProperty, "ThemeTextPrimaryBrush");
                        SetThemeBrush(dayText, TextBlock.ForegroundProperty, "ThemeTextPrimaryBrush");
                        dayButton.IsEnabled = true;
                        dayButton.Cursor = System.Windows.Input.Cursors.Hand;
                        dayButton.ToolTip = $"{DefaultLanguage.AddTaskButton}/{DefaultLanguage.NoTasks}";
                    }

                    dayButton.Click += (s, e) =>
                    {
                        SelectDate(cellDate);
                    };
                }
                else
                {
                    dayButton.Opacity = 0.3;
                    dayText.Text = "";
                    dayButton.IsEnabled = false;
                }

                dayBorder.Child = dayButton;
                calendarGrid.Children.Add(dayBorder);
            }
        }

        private string GetLocalizedMonthName(int month)
        {
            var dateForMonth = new DateTime(2024, month, 1);
            var currentLang = ConfigService.Instance?.Language ?? "en";

            if (currentLang == "uk")
            {
                var culture = new CultureInfo("uk-UA");
                return dateForMonth.ToString("MMMM", culture).ToUpper();
            }
            else
            {
                var culture = new CultureInfo("en-US");
                return dateForMonth.ToString("MMMM", culture).ToUpper();
            }
        }

        private void ShowTaskActionsContextMenu(FrameworkElement placementTarget, MenuItem sourceMenuItem, RemindingDto task, ContextMenu existing = null)
        {
            ContextMenu cm = existing ?? new ContextMenu();

            bool isPastTask = IsPastDate(task.LastTime);

            if (!isPastTask)
            {
                var edit = new MenuItem { Header = DefaultLanguage.ContextMenuEdit, Tag = task };
                edit.Click += (s, e) => EditTask(task);
                cm.Items.Add(edit);
            }
            else
            {
                var view = new MenuItem { Header = DefaultLanguage.ContextMenuView, Tag = task, IsEnabled = false };
                cm.Items.Add(view);
            }

            if (!isPastTask)
            {
                var duplicate = new MenuItem { Header = DefaultLanguage.ContextMenuDuplicate, Tag = task };
                duplicate.Click += (s, e) => DuplicateTask(task);
                cm.Items.Add(duplicate);
            }

            var del = new MenuItem { Header = DefaultLanguage.ContextMenuDelete, Tag = task };
            SetThemeBrush(del, Control.ForegroundProperty, "ThemeDangerBrush");
            del.Click += (s, e) => DeleteTask(task);
            cm.Items.Add(del);

            if (existing == null && sourceMenuItem != null)
            {
                try { sourceMenuItem.IsSubmenuOpen = true; } catch { }
            }

            if (placementTarget != null)
            {
                cm.PlacementTarget = placementTarget;
                cm.Placement = PlacementMode.MousePoint;
                cm.IsOpen = true;
            }
            else
            {
                cm.Placement = PlacementMode.MousePoint;
                cm.IsOpen = true;
            }
        }

        private void EditTask(RemindingDto task)
        {
            _selectedDateForTask = task.LastTime.Date;

            if (IsPastDate(_selectedDateForTask))
            {
                MessageBox.Show(DefaultLanguage.PastTaskMessage,
                              DefaultLanguage.PastTask,
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                return;
            }

            var title = Get<TextBox>("AddTaskTitle");
            var desc = Get<TextBox>("AddTaskDescription");
            var hasTimeChk = Get<CheckBox>("AddTaskHasTime");
            var timeBorder = Get<Border>("AddTaskTimeBorder");
            var timeBox = Get<TextBox>("AddTaskTime");

            if (title != null) title.Text = task.Name;
            if (desc != null) desc.Text = task.Content;

            if (hasTimeChk != null) hasTimeChk.IsChecked = task.ShowTime;
            if (timeBorder != null) timeBorder.Visibility = task.ShowTime ? Visibility.Visible : Visibility.Collapsed;

            if (timeBox != null)
            {
                timeBox.Text = task.ShowTime ? task.LastTime.ToString("HH:mm") : "09:00";
            }

            string taskColor = GetCategoryColor(task.Type);

            var r = Get<RadioButton>("ColorRed"); if (r != null && taskColor == "#FF6B6B") r.IsChecked = true;
            var b = Get<RadioButton>("ColorBlue"); if (b != null && taskColor == "#4A6CF7") b.IsChecked = true;
            var g = Get<RadioButton>("ColorGreen"); if (g != null && taskColor == "#32CD32") g.IsChecked = true;
            var o = Get<RadioButton>("ColorOrange"); if (o != null && taskColor == "#FFA500") o.IsChecked = true;
            var p = Get<RadioButton>("ColorPurple"); if (p != null && taskColor == "#9B59B6") p.IsChecked = true;
            var pi = Get<RadioButton>("ColorPink"); if (pi != null && taskColor == "#FF69B4") pi.IsChecked = true;

            var panel = Get<Border>("EditTaskPanel");
            var overlay = Get<Rectangle>("OverlayBackground");
            OpenEditTaskPanel(task);
            if (overlay != null) overlay.Visibility = Visibility.Visible;

            if (panel != null) panel.Tag = task;
        }

        private async void DeleteTask(RemindingDto task)
        {
            var res = MessageBox.ShowQuestion($"{DefaultLanguage.DeleteTaskConfirm} '{task.Name}'?", DefaultLanguage.Confirm);
            if (res == MessageBoxResult.Yes)
            {
                _tasks.Remove(task);
                await _apiClient.DeleteRemindingAsync(task.Id);
                await UpdateCalendarAsync();
                UpdateTasksList(_selectedDateForTask);
            }
        }

        private async void DuplicateTask(RemindingDto task)
        {
            var wnd = new Window
            {
                Title = DefaultLanguage.DuplicateTaskTitle,
                Width = 400,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current.MainWindow,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(12) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = DefaultLanguage.DuplicateTargetDate,
                Style = GetStyle("BodyTextStyle"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var datePicker = new System.Windows.Controls.DatePicker
            {
                SelectedDate = DateTime.Today,
                DisplayDateStart = DateTime.Today
            };
            Grid.SetRow(datePicker, 1);
            grid.Children.Add(datePicker);

            var repeatPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
            var noneRb = new RadioButton { Content = DefaultLanguage.DuplicateOnce, IsChecked = true, GroupName = "dup" };
            var daily = new RadioButton { Content = DefaultLanguage.DuplicateDaily, GroupName = "dup" };
            var weekly = new RadioButton { Content = DefaultLanguage.DuplicateWeekly, GroupName = "dup" };
            var monthly = new RadioButton { Content = DefaultLanguage.DuplicateMonthly, GroupName = "dup" };
            repeatPanel.Children.Add(noneRb); repeatPanel.Children.Add(daily); repeatPanel.Children.Add(weekly); repeatPanel.Children.Add(monthly);
            Grid.SetRow(repeatPanel, 2);
            grid.Children.Add(repeatPanel);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button
            {
                Content = DefaultLanguage.CancelTaskButton,
                Width = 80,
                Style = GetStyle("SecondaryButtonStyle"),
                Margin = new Thickness(0, 0, 8, 0)
            };
            cancel.Click += (s, e) => wnd.DialogResult = false;
            var ok = new Button
            {
                Content = DefaultLanguage.SaveTaskButton,
                Width = 80,
                Style = GetStyle("PrimaryButtonStyle")
            };
            ok.Click += (s, e) => wnd.DialogResult = true;
            btns.Children.Add(cancel); btns.Children.Add(ok);
            Grid.SetRow(btns, 3);
            grid.Children.Add(btns);

            wnd.Content = grid;

            if (wnd.ShowDialog() == true)
            {
                var targetDate = datePicker.SelectedDate ?? DateTime.Today;

                if (IsPastDate(targetDate))
                {
                    MessageBox.Show(DefaultLanguage.CannotDuplicatePastDate,
                                   DefaultLanguage.InvalidDate,
                                   MessageBoxButton.OK,
                                   MessageBoxImage.Warning);
                    return;
                }

                var dup = new CreateRemindingDto
                {
                    Type = task.Type,
                    Name = task.Name,
                    Content = task.Content,
                    LastTime = targetDate,
                    ShouldNotify = task.ShouldNotify,
                    ShowTime = task.ShowTime,
                };
                await _apiClient.CreateRemindingAsync(dup);

                if (daily.IsChecked == true)
                {
                    for (int i = 1; i <= 6; i++)
                    {
                        var d = new CreateRemindingDto
                        {
                            Type = task.Type,
                            Name = task.Name,
                            Content = task.Content,
                            LastTime = dup.LastTime.AddDays(i),
                            ShouldNotify = task.ShouldNotify,
                            ShowTime = task.ShowTime,
                        };
                        await _apiClient.CreateRemindingAsync(d);
                    }
                }
                else if (weekly.IsChecked == true)
                {
                    for (int i = 1; i <= 4; i++)
                    {
                        var d = new CreateRemindingDto
                        {
                            Type = task.Type,
                            Name = task.Name,
                            Content = task.Content,
                            LastTime = dup.LastTime.AddDays(7 * i),
                            ShouldNotify = task.ShouldNotify,
                            ShowTime = task.ShowTime,
                            IsCompleted = false
                        };
                        await _apiClient.CreateRemindingAsync(d);
                    }
                }
                else if (monthly.IsChecked == true)
                {
                    for (int i = 1; i <= 3; i++)
                    {
                        var d = new CreateRemindingDto
                        {
                            Type = task.Type,
                            Name = task.Name,
                            Content = task.Content,
                            LastTime = dup.LastTime.AddMonths(i),
                            ShouldNotify = task.ShouldNotify,
                            ShowTime = task.ShowTime,
                            IsCompleted = false
                        };
                        await _apiClient.CreateRemindingAsync(d);
                    }
                }

                await UpdateCalendarAsync();
                UpdateTasksList(_selectedDateForTask);
            }
        }

        private void SelectDate(DateTime date)
        {
            _selectedDateForTask = date;

            var tasksHeader = Get<TextBlock>("TasksHeader");
            if (tasksHeader != null)
            {
                if (date.Date == DateTime.Today.Date)
                    tasksHeader.Text = DefaultLanguage.TodaysTasks;
                else if (IsPastDate(date))
                    tasksHeader.Text = string.Format(DefaultLanguage.PastTasksFor, date.ToString("MM/dd/yyyy"));
                else
                    tasksHeader.Text = string.Format(DefaultLanguage.TasksForDate, date.ToString("MM/dd/yyyy"));
            }

            UpdateTasksList(date);
        }

        private void UpdateTasksList(DateTime? selectedDate = null)
        {
            var tasksPanel = Get<StackPanel>("TasksPanel");
            if (tasksPanel == null) return;

            tasksPanel.Children.Clear();

            DateTime targetDate = selectedDate ?? DateTime.Today;
            var filter = _currentFilter;

            if (filter == DefaultLanguage.FilterActive)
            {
                var tasksToShow = _tasks.Where(t => t.LastTime.Date == targetDate.Date && !t.IsCompleted).OrderBy(t => t.ShowTime ? t.LastTime.TimeOfDay : TimeSpan.Zero).ToList();
                RenderTasksList(tasksPanel, tasksToShow, selectedDate);
                return;
            }
            else if (filter == DefaultLanguage.FilterCompleted)
            {
                var tasksToShow = _tasks.Where(t => t.LastTime.Date == targetDate.Date && t.IsCompleted).OrderBy(t => t.ShowTime ? t.LastTime.TimeOfDay : TimeSpan.Zero).ToList();
                RenderTasksList(tasksPanel, tasksToShow, selectedDate);
                return;
            }
            else if (filter == DefaultLanguage.FilterUpcoming)
            {
                var upcoming = _tasks.Where(t => t.LastTime.Date >= DateTime.Today && !t.IsCompleted)
                                     .OrderBy(t => t.LastTime.Date)
                                     .ThenBy(t => t.ShowTime ? t.LastTime.TimeOfDay : TimeSpan.Zero)
                                     .ToList();

                if (!upcoming.Any())
                {
                    var noUpcomingTasksText = new TextBlock
                    {
                        Text = DefaultLanguage.NoUpcomingTasks,
                        Style = GetStyle("BodyTextStyle"),
                        FontSize = 16,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 0)
                    };
                    SetThemeBrush(noUpcomingTasksText, TextBlock.ForegroundProperty, "ThemeTextSecondaryBrush");
                    tasksPanel.Children.Add(noUpcomingTasksText);
                    return;
                }

                var groups = upcoming.GroupBy(t => t.LastTime.Date);
                foreach (var g in groups)
                {
                    var header = new TextBlock
                    {
                        Text = g.Key == DateTime.Today ? DefaultLanguage.Today : g.Key.ToString("dddd, MMM d"),
                        Style = GetStyle("BodyBoldTextStyle"),
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 10, 0, 6)
                    };
                    SetThemeBrush(header, TextBlock.ForegroundProperty, "ThemeTextPrimaryBrush");
                    tasksPanel.Children.Add(header);

                    RenderTasksList(tasksPanel, g.OrderBy(t => t.ShowTime ? t.LastTime.TimeOfDay : TimeSpan.Zero).ToList(), null);
                }

                return;
            }

            var tasksForDate = _tasks.Where(t => t.LastTime.Date == targetDate.Date).OrderBy(t => t.ShowTime ? t.LastTime.TimeOfDay : TimeSpan.Zero).ToList();
            RenderTasksList(tasksPanel, tasksForDate, selectedDate);
        }

        private void RenderTasksList(Panel tasksPanel, List<RemindingDto> tasksToShow, DateTime? selectedDate)
        {
            if (!tasksToShow.Any())
            {
                TextBlock noTasksText = new TextBlock
                {
                    Text = DefaultLanguage.NoTasks,
                    Style = GetStyle("BodyTextStyle"),
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                SetThemeBrush(noTasksText, TextBlock.ForegroundProperty, "ThemeTextSecondaryBrush");
                tasksPanel.Children.Add(noTasksText);
                return;
            }

            foreach (var task in tasksToShow)
            {
                Border taskCard = new Border
                {
                    Style = GetStyle("TaskItemStyle"),
                    CornerRadius = new CornerRadius(10)
                };

                Grid taskGrid = new Grid();
                taskGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                taskGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                taskGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                StackPanel colorPanel = new StackPanel
                {
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                string taskColor = GetCategoryColor(task.Type);

                Border colorIndicator = new Border
                {
                    Width = 12,
                    Height = 12,
                    CornerRadius = new CornerRadius(6),
                    Background = CreateColorBrush(taskColor),
                    Margin = new Thickness(0, 0, 0, 4)
                };

                TextBlock categoryText = new TextBlock
                {
                    Text = GetLocalizedCategoryName(task.Type),
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.7
                };
                SetThemeBrush(categoryText, TextBlock.ForegroundProperty, "ThemeTextSecondaryBrush");

                colorPanel.Children.Add(colorIndicator);
                colorPanel.Children.Add(categoryText);

                Grid.SetColumn(colorPanel, 0);
                taskGrid.Children.Add(colorPanel);

                StackPanel textPanel = new StackPanel();

                TextBlock timeText = new TextBlock
                {
                    Text = task.ShowTime ? task.LastTime.ToString("HH:mm") : string.Empty,
                    FontSize = 12,
                    Opacity = 0.9,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                SetThemeBrush(timeText, TextBlock.ForegroundProperty, GetTaskTextResourceKey(task));

                TextBlock titleText = new TextBlock
                {
                    Text = task.Name,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    TextDecorations = task.IsCompleted ? TextDecorations.Strikethrough : null,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                SetThemeBrush(titleText, TextBlock.ForegroundProperty, GetTaskTextResourceKey(task));

                if (!string.IsNullOrEmpty(task.Content))
                {
                    TextBlock descText = new TextBlock
                    {
                        Text = task.Content,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        TextDecorations = task.IsCompleted ? TextDecorations.Strikethrough : null,
                        Opacity = 0.8
                    };
                    SetThemeBrush(descText, TextBlock.ForegroundProperty, GetTaskTextResourceKey(task));
                    textPanel.Children.Add(descText);
                }

                textPanel.Children.Add(timeText);
                textPanel.Children.Add(titleText);
                Grid.SetColumn(textPanel, 1);
                taskGrid.Children.Add(textPanel);

                CheckBox doneCheckbox = new CheckBox
                {
                    IsChecked = task.IsCompleted,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    Content = ""
                };
                doneCheckbox.Checked += async (s, e) =>
                {
                    task.IsCompleted = true;
                    await _apiClient.ToggleRemindingAsync(task.Id);
                };
                doneCheckbox.Unchecked += async (s, e) =>
                {
                    task.IsCompleted = false;
                    await _apiClient.ToggleRemindingAsync(task.Id);
                };
                Grid.SetColumn(doneCheckbox, 2);
                taskGrid.Children.Add(doneCheckbox);

                taskCard.Child = taskGrid;

                taskCard.MouseRightButtonUp += (s, e) =>
                {
                    var card = s as Border;
                    ShowTaskActionsContextMenu(card, null, task, null);
                };

                tasksPanel.Children.Add(taskCard);
            }
        }

    }
}
