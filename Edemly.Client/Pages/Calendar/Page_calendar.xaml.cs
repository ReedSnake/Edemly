#nullable disable

using Edemly.Client.Api;
using Edemly.Client.Lang;
using Edemly.Client.Services;
using Edemly.Contracts.Remindings;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Edemly.Client
{
    public partial class Page_calendar : Page
    {
        private static readonly Dictionary<int, (string Color, string Category)> _remindingTypeMap =
        new()
        {
            { 0, ("#FF6B6B", DefaultLanguage.CategoryImportant) },
            { 1, ("#4A6CF7", DefaultLanguage.CategoryWork) },
            { 2, ("#32CD32", DefaultLanguage.CategoryPersonal) },
            { 3, ("#FFA500", DefaultLanguage.CategorySports) },
            { 4, ("#9B59B6", DefaultLanguage.CategoryStudy) },
            { 5, ("#FF69B4", DefaultLanguage.CategoryEntertainment) }
        };

        private static readonly Dictionary<string, int> _getRemindingTypeByCategory =
            _remindingTypeMap.ToDictionary(x => x.Value.Category, x => x.Key);

        private Popup _dayTasksPopup;

        private DateTime _currentDate;
        private List<RemindingDto> _tasks = new List<RemindingDto>();
        private DateTime _selectedDateForTask;
        private string _currentFilter = string.Empty;
        private readonly IApiService _apiService;

        public Page_calendar()
        {
            InitializeComponent();

            _dayTasksPopup = new Popup
            {
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                StaysOpen = false
            };

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
            _apiService = App.ApiService;

            ApplyLocalization();

            _currentFilter = DefaultLanguage.FilterAll;

            ThemeService.Instance.ThemeChanged += (themeName) => OnThemeChanged();

            ApplyThemeToPage();

            _ = UpdateCalendarAsync();
            UpdateTasksList();
            UpdateFilterButtonsStyle(_currentFilter);
        }

        private void OnThemeChanged()
        {
            try
            {
                ApplyThemeToPage();
                UpdateFilterButtonsStyle(_currentFilter);
                _ = UpdateCalendarAsync();
                UpdateTasksList();
            }
            catch { }
        }

        private void ApplyThemeToPage()
        {
            try
            {
                var palette = ThemeService.Instance.GetCurrentPalette();

                var grid = this.Content as Grid;
                if (grid != null)
                {
                    var gradientBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(1, 1),
                        EndPoint = new Point(0, 0)
                    };
                    gradientBrush.GradientStops.Add(new GradientStop(palette.BackgroundDark, 0.7));
                    gradientBrush.GradientStops.Add(new GradientStop(palette.Primary, 0.0));
                    grid.Background = gradientBrush;

                    foreach (var child in grid.Children)
                    {
                        if (child is Rectangle rect)
                        {
                            rect.Fill = new SolidColorBrush(palette.Background);
                        }
                    }
                }

                var monthText = Get<TextBlock>("MonthText");
                var yearText = Get<TextBlock>("YearText");
                var tasksHeader = Get<TextBlock>("TasksHeader");
                var showFilterLabel = Get<TextBlock>("ShowFilterLabel");

                if (monthText != null) monthText.Foreground = new SolidColorBrush(palette.Secondary);
                if (yearText != null) yearText.Foreground = new SolidColorBrush(palette.Secondary);
                if (tasksHeader != null) tasksHeader.Foreground = new SolidColorBrush(palette.Secondary);
                if (showFilterLabel != null) showFilterLabel.Foreground = new SolidColorBrush(palette.Secondary);

                if (TodayBtn != null)
                {
                    TodayBtn.Background = new SolidColorBrush(palette.Secondary);
                }

                var addTaskBtn = Get<Button>("AddTaskBtn");
                if (addTaskBtn != null)
                {
                    addTaskBtn.Foreground = new SolidColorBrush(palette.Secondary);
                }

                var backBtn = Get<Button>("BackButton");
                if (backBtn != null)
                {
                    backBtn.Foreground = new SolidColorBrush(palette.Secondary);
                }

                var prevBtn = Get<Button>("PrevMonthBtn");
                var nextBtn = Get<Button>("NextMonthBtn");
                if (prevBtn != null) prevBtn.Foreground = new SolidColorBrush(palette.Secondary);
                if (nextBtn != null) nextBtn.Foreground = new SolidColorBrush(palette.Secondary);

                var dayTexts = new[] { "DaySunText", "DayMonText", "DayTueText", "DayWedText", "DayThuText", "DayFriText", "DaySatText" };
                foreach (var dayName in dayTexts)
                {
                    var dayText = Get<TextBlock>(dayName);
                    if (dayText != null) dayText.Foreground = new SolidColorBrush(palette.Secondary);
                }

                var saveAddTaskBtn = Get<Button>("SaveAddTaskBtn");
                var cancelAddTaskBtn = Get<Button>("CancelAddTaskBtn");
                var saveEditTaskBtn = Get<Button>("SaveEditTaskBtn");
                var cancelEditTaskBtn = Get<Button>("CancelEditTaskBtn");

                if (saveAddTaskBtn != null) saveAddTaskBtn.Background = new SolidColorBrush(palette.Primary);
                if (saveEditTaskBtn != null) saveEditTaskBtn.Background = new SolidColorBrush(palette.Primary);

                System.Diagnostics.Debug.WriteLine($"[PAGE_CALENDAR] Theme applied: {ThemeService.Instance.CurrentTheme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_CALENDAR] ApplyThemeToPage error: {ex.Message}");
            }
        }

        private void ApplyLocalization()
        {
            if (TodayBtn != null) TodayBtn.Content = DefaultLanguage.TodayButton;

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

        private int GetCategoryTypeByName(string categoryName)
        {
            if (categoryName == DefaultLanguage.CategoryImportant) return 0;
            if (categoryName == DefaultLanguage.CategoryWork) return 1;
            if (categoryName == DefaultLanguage.CategoryPersonal) return 2;
            if (categoryName == DefaultLanguage.CategorySports) return 3;
            if (categoryName == DefaultLanguage.CategoryStudy) return 4;
            if (categoryName == DefaultLanguage.CategoryEntertainment) return 5;
            return 1;
        }

        private T Get<T>(string name) where T : class
        {
            return this.FindName(name) as T;
        }

        private bool IsPastDate(DateTime date)
        {
            return date.Date < DateTime.Today.Date;
        }

        private void CategoryScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var sv = sender as ScrollViewer;
            if (sv == null) return;

            double newOffset = sv.HorizontalOffset - e.Delta;
            if (newOffset < 0) newOffset = 0;
            if (newOffset > sv.ScrollableWidth) newOffset = sv.ScrollableWidth;
            sv.ScrollToHorizontalOffset(newOffset);
            e.Handled = true;
        }

        private void TimeTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            string currentText = textBox.Text;
            int caretIndex = textBox.CaretIndex;

            if (!char.IsDigit(e.Text[0]) && e.Text != ":")
            {
                e.Handled = true;
                return;
            }

            if (caretIndex == 2 && e.Text != ":")
            {
                textBox.Text = currentText.Insert(2, ":");
                textBox.CaretIndex = 3;

                if (!string.IsNullOrEmpty(e.Text))
                {
                    string newText = textBox.Text.Insert(3, e.Text);
                    if (newText.Length > 5) newText = newText.Substring(0, 5);
                    textBox.Text = newText;
                    textBox.CaretIndex = 4;
                }

                e.Handled = true;
            }
            else if (caretIndex == 2 && e.Text == ":")
            {
                textBox.CaretIndex = 3;
                e.Handled = true;
            }
        }

        private void TimeTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (e.Key == System.Windows.Input.Key.Back)
            {
                if (textBox.CaretIndex == 3 && textBox.Text.Length > 3 && textBox.Text[2] == ':')
                {
                    textBox.Text = textBox.Text.Remove(3, 1);
                    textBox.CaretIndex = 3;
                    e.Handled = true;
                }
                else if (textBox.CaretIndex == 2 && textBox.Text.Length > 2 && textBox.Text[2] == ':')
                {
                    textBox.Text = textBox.Text.Remove(2, 1);
                    textBox.CaretIndex = 2;
                    e.Handled = true;
                }
            }
            else if (e.Key == System.Windows.Input.Key.Up || e.Key == System.Windows.Input.Key.Down)
            {
                e.Handled = true;

                string[] parts = textBox.Text.Split(':');
                if (parts.Length != 2) return;

                if (!int.TryParse(parts[0], out int hours) || !int.TryParse(parts[1], out int minutes))
                    return;

                bool editingHours = textBox.CaretIndex <= 2;

                if (editingHours)
                {
                    if (e.Key == System.Windows.Input.Key.Up)
                        hours = (hours + 1) % 24;
                    else
                        hours = (hours - 1 + 24) % 24;
                }
                else
                {
                    if (e.Key == System.Windows.Input.Key.Up)
                        minutes = (minutes + 1) % 60;
                    else
                        minutes = (minutes - 1 + 60) % 60;
                }

                textBox.Text = $"{hours:00}:{minutes:00}";

                if (editingHours)
                    textBox.CaretIndex = 2;
                else
                    textBox.CaretIndex = 5;
            }
            else if (e.Key == System.Windows.Input.Key.Tab)
            {
                if (textBox.CaretIndex <= 2)
                {
                    textBox.CaretIndex = 3;
                    e.Handled = true;
                }
            }
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
                _tasks = await _apiService.GetMyRemindingsAsync();
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
                    Style = (Style)FindResource("DayButtonStyle"),
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
                    Margin = new Thickness(0, 3, 3, 0),
                    Foreground = new SolidColorBrush(ThemeService.Instance.GetCurrentPalette().Secondary)
                };

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
                        string taskColor = task.Type switch
                        {
                            0 => "#FF6B6B",
                            1 => "#4A6CF7",
                            2 => "#32CD32",
                            3 => "#FFA500",
                            4 => "#9B59B6",
                            5 => "#FF69B4",
                            _ => "#4A6CF7"
                        };

                        Ellipse dot = new Ellipse
                        {
                            Width = 6,
                            Height = 6,
                            Margin = new Thickness(3, 0, 3, 0),
                            Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(taskColor))
                        };
                        taskIndicator.Children.Add(dot);
                    }

                    bool isPastDate = IsPastDate(cellDate);
                    bool isToday = cellDate.Date == DateTime.Today.Date;

                    if (isToday)
                    {
                        dayButton.Background = new SolidColorBrush(ThemeService.Instance.GetCurrentPalette().Secondary);
                        dayButton.Foreground = Brushes.White;
                        dayText.Foreground = Brushes.White;
                        dayButton.IsEnabled = true;
                        dayButton.Cursor = System.Windows.Input.Cursors.Hand;
                        dayButton.ToolTip = $"{DefaultLanguage.Today} - {DefaultLanguage.AddTaskButton}/{DefaultLanguage.NoTasks}";
                    }
                    else if (isPastDate)
                    {
                        dayButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"));
                        dayButton.Foreground = Brushes.Gray;
                        dayText.Foreground = Brushes.Gray;
                        dayButton.Opacity = 0.7;
                        dayButton.IsEnabled = true;
                        dayButton.Cursor = System.Windows.Input.Cursors.Hand;
                        dayButton.ToolTip = $"{DefaultLanguage.NoTasks} ({DefaultLanguage.CannotAddPastDate})";
                        foreach (Ellipse dot in taskIndicator.Children)
                            dot.Fill = Brushes.Gray;
                    }
                    else
                    {
                        dayButton.Background = new SolidColorBrush(ThemeService.Instance.GetCurrentPalette().Background);
                        dayButton.Foreground = new SolidColorBrush(ThemeService.Instance.GetCurrentPalette().TextPrimary);
                        dayText.Foreground = new SolidColorBrush(ThemeService.Instance.GetCurrentPalette().TextPrimary);
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

            var del = new MenuItem { Header = DefaultLanguage.ContextMenuDelete, Tag = task, Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69)) };
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

            string taskColor = task.Type switch
            {
                0 => "#FF6B6B",
                1 => "#4A6CF7",
                2 => "#32CD32",
                3 => "#FFA500",
                4 => "#9B59B6",
                5 => "#FF69B4",
                _ => "#4A6CF7"
            };

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
            var res = MessageBox.Show($"{DefaultLanguage.DeleteTaskConfirm} '{task.Name}'?", DefaultLanguage.Confirm, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                _tasks.Remove(task);
                await _apiService.DeleteRemindingAsync(task.Id);
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
                Owner = Application.Current.MainWindow,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(12) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = DefaultLanguage.DuplicateTargetDate, Margin = new Thickness(0, 0, 0, 8) };
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
            var cancel = new Button { Content = DefaultLanguage.CancelTaskButton, Width = 80, Margin = new Thickness(0, 0, 8, 0) };
            cancel.Click += (s, e) => wnd.DialogResult = false;
            var ok = new Button { Content = DefaultLanguage.SaveTaskButton, Width = 80 };
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
                await _apiService.CreateRemindingAsync(dup);

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
                        await _apiService.CreateRemindingAsync(d);
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
                        await _apiService.CreateRemindingAsync(d);
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
                        await _apiService.CreateRemindingAsync(d);
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
                    tasksPanel.Children.Add(new TextBlock { Text = DefaultLanguage.NoUpcomingTasks, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B4539")), FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) });
                    return;
                }

                var groups = upcoming.GroupBy(t => t.LastTime.Date);
                foreach (var g in groups)
                {
                    var header = new TextBlock
                    {
                        Text = g.Key == DateTime.Today ? DefaultLanguage.Today : g.Key.ToString("dddd, MMM d"),
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B4539")),
                        Margin = new Thickness(0, 10, 0, 6)
                    };
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
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B4539")),
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                tasksPanel.Children.Add(noTasksText);
                return;
            }

            foreach (var task in tasksToShow)
            {
                Border taskCard = new Border
                {
                    Style = (Style)FindResource("TaskItemStyle"),
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

                string taskColor = task.Type switch
                {
                    0 => "#FF6B6B",
                    1 => "#4A6CF7",
                    2 => "#32CD32",
                    3 => "#FFA500",
                    4 => "#9B59B6",
                    5 => "#FF69B4",
                    _ => "#4A6CF7"
                };

                Border colorIndicator = new Border
                {
                    Width = 12,
                    Height = 12,
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(taskColor)),
                    Margin = new Thickness(0, 0, 0, 4)
                };

                TextBlock categoryText = new TextBlock
                {
                    Text = GetLocalizedCategoryName(task.Type),
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B4539")),
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.7
                };

                colorPanel.Children.Add(colorIndicator);
                colorPanel.Children.Add(categoryText);

                Grid.SetColumn(colorPanel, 0);
                taskGrid.Children.Add(colorPanel);

                StackPanel textPanel = new StackPanel();

                TextBlock timeText = new TextBlock
                {
                    Text = task.ShowTime ? task.LastTime.ToString("HH:mm") : string.Empty,
                    Foreground = task.IsCompleted ? new SolidColorBrush(Colors.Gray) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B4539")),
                    FontSize = 12,
                    Opacity = 0.9,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                TextBlock titleText = new TextBlock
                {
                    Text = task.Name,
                    Foreground = task.IsCompleted ?
                        new SolidColorBrush(Colors.Gray) :
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B4539")),
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    TextDecorations = task.IsCompleted ? TextDecorations.Strikethrough : null,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                if (!string.IsNullOrEmpty(task.Content))
                {
                    TextBlock descText = new TextBlock
                    {
                        Text = task.Content,
                        Foreground = task.IsCompleted ?
                            new SolidColorBrush(Colors.Gray) :
                            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B4539")),
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                        TextDecorations = task.IsCompleted ? TextDecorations.Strikethrough : null,
                        Opacity = 0.8
                    };
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
                    await _apiService.ToggleRemindingAsync(task.Id);
                };
                doneCheckbox.Unchecked += async (s, e) =>
                {
                    task.IsCompleted = false;
                    await _apiService.ToggleRemindingAsync(task.Id);
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

            string taskColor = task.Type switch
            {
                0 => "#FF6B6B",
                1 => "#4A6CF7",
                2 => "#32CD32",
                3 => "#FFA500",
                4 => "#9B59B6",
                5 => "#FF69B4",
                _ => "#4A6CF7"
            };

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

                    if (!DateTime.TryParseExact(timeText, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime) &&
                        !DateTime.TryParseExact(timeText, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedTime))
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

            int selectedType = 1; // Default to Work

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
                    var createdReminding = await _apiService.CreateRemindingAsync(newTask);
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

                if (!DateTime.TryParseExact(
                        timeBox.Text.Trim(),
                        new[] { "HH:mm", "H:mm" },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime parsed))
                {
                    MessageBox.Show(DefaultLanguage.InvalidTimeFormat, DefaultLanguage.InvalidDate,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    timeBox.Focus();
                    timeBox.SelectAll();
                    return;
                }

                hour = parsed.Hour;
                minute = parsed.Minute;
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

            await _apiService.UpdateRemindingAsync(model);

            await UpdateCalendarAsync();
            UpdateTasksList(_selectedDateForTask);

            HideEditTaskPanel();
        }

        private void CancelAddTaskBtn_Click(object sender, RoutedEventArgs e) => HideAddTaskPanel();

        private void CloseAddPanelBtn_Click(object sender, RoutedEventArgs e) => HideAddTaskPanel();

        private void CancelEditTaskBtn_Click(object sender, RoutedEventArgs e) => HideEditTaskPanel();

        private void CloseEditPanelBtn_Click(object sender, RoutedEventArgs e) => HideEditTaskPanel();

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            string filter = button.Tag as string ?? DefaultLanguage.FilterAll;
            _currentFilter = filter;

            UpdateFilterButtonsStyle(filter);
            UpdateTasksList(_selectedDateForTask);
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

            var themePalette = ThemeService.Instance.GetCurrentPalette();

            foreach (var btn in allButtons)
            {
                if (btn == null) continue;

                string btnFilter = btn.Tag as string ?? DefaultLanguage.FilterAll;

                if (btnFilter == activeFilter)
                {
                    btn.Background = new SolidColorBrush(themePalette.Secondary);
                    btn.Foreground = Brushes.White;
                    btn.BorderBrush = Brushes.Transparent;
                    btn.FontWeight = FontWeights.Bold;
                }
                else
                {
                    btn.Background = new SolidColorBrush(themePalette.Background);
                    btn.Foreground = new SolidColorBrush(themePalette.TextPrimary);
                    btn.BorderBrush = new SolidColorBrush(themePalette.BorderLight);
                    btn.BorderThickness = new Thickness(1);
                    btn.FontWeight = FontWeights.SemiBold;
                }
            }
        }

        private void AddTaskHasTime_Checked(object sender, RoutedEventArgs e)
        {
            var timeBorder = Get<Border>("AddTaskTimeBorder");
            if (timeBorder != null)
                timeBorder.Visibility = Visibility.Visible;
        }

        private void AddTaskHasTime_Unchecked(object sender, RoutedEventArgs e)
        {
            var timeBorder = Get<Border>("AddTaskTimeBorder");
            if (timeBorder != null)
                timeBorder.Visibility = Visibility.Collapsed;
        }

        private void EditTaskHasTime_Checked(object sender, RoutedEventArgs e)
        {
            var timeBorder = Get<Border>("EditTaskTimeBorder");
            if (timeBorder != null)
                timeBorder.Visibility = Visibility.Visible;
        }

        private void EditTaskHasTime_Unchecked(object sender, RoutedEventArgs e)
        {
            var timeBorder = Get<Border>("EditTaskTimeBorder");
            if (timeBorder != null)
                timeBorder.Visibility = Visibility.Collapsed;
        }

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

        private void BackButton_Click(object sender, RoutedEventArgs e) => NavigationService.GoBack();

        private void TodayBtn_Click(object sender, RoutedEventArgs e)
        { _currentDate = DateTime.Today; _ = UpdateCalendarAsync(); SelectDate(DateTime.Today); }

        private void PrevMonthBtn_Click(object sender, RoutedEventArgs e)
        { _currentDate = _currentDate.AddMonths(-1); _ = UpdateCalendarAsync(); }

        private void NextMonthBtn_Click(object sender, RoutedEventArgs e)
        { _currentDate = _currentDate.AddMonths(1); _ = UpdateCalendarAsync(); }
    }
}