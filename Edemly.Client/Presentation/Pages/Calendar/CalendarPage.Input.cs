#nullable disable

using Edemly.Client.Presentation.Pages.Calendar.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Edemly.Client.Presentation.Pages.Calendar
{
    public partial class CalendarPage
    {
        private void CategoryScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var sv = sender as ScrollViewer;
            if (sv == null) return;

            double newOffset = sv.HorizontalOffset - e.Delta;
            if (newOffset < 0) newOffset = 0;
            if (newOffset > sv.ScrollableWidth) newOffset = sv.ScrollableWidth;
            sv.ScrollToHorizontalOffset(newOffset);
            e.Handled = true;
        }

        private void TimeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = CalendarPageTimeInputHelper.HandleTextInput(sender as TextBox, e.Text);
        }

        private void TimeTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (CalendarPageTimeInputHelper.HandlePreviewKeyDown(sender as TextBox, e.Key))
            {
                e.Handled = true;
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

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            string filter = button.Tag as string ?? DefaultLanguage.FilterAll;
            _currentFilter = filter;

            UpdateFilterButtonsStyle(filter);
            UpdateTasksList(_selectedDateForTask);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) => NavigationService.GoBack();

        private void TodayBtn_Click(object sender, RoutedEventArgs e)
        {
            _currentDate = DateTime.Today;
            _ = UpdateCalendarAsync();
            SelectDate(DateTime.Today);
        }

        private void PrevMonthBtn_Click(object sender, RoutedEventArgs e)
        {
            _currentDate = _currentDate.AddMonths(-1);
            _ = UpdateCalendarAsync();
        }

        private void NextMonthBtn_Click(object sender, RoutedEventArgs e)
        {
            _currentDate = _currentDate.AddMonths(1);
            _ = UpdateCalendarAsync();
        }
    }
}
