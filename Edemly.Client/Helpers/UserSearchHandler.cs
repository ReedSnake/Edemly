#nullable disable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Edemly.Client.DTOs;
using Edemly.Client.Services.Api;

namespace Edemly.Client.Helpers
{
    public class UserSearchHandler
    {
        private readonly IApiService _apiService;
        private readonly ChatUIBuilder _uiBuilder;
        private readonly int _currentUserId;

        public UserSearchHandler(IApiService apiService, int currentUserId)
        {
            _apiService = apiService;
            _currentUserId = currentUserId;
            _uiBuilder = new ChatUIBuilder();
        }

        // New constructor that uses App.ApiService
        public UserSearchHandler(int currentUserId)
        {
            _apiService = App.ApiService ?? throw new InvalidOperationException("App.ApiService is not initialized");
            _currentUserId = currentUserId;
            _uiBuilder = new ChatUIBuilder();
        }

        public async Task SearchAndDisplayResultsAsync(
            string searchText,
            TextBox searchTextBox,
            StackPanel resultsPanel,
            Func<UserDto, Task> onUserSelected)
        {
            var scrollViewer = resultsPanel.Parent as ScrollViewer;
            var grid = scrollViewer?.Parent as Grid;
            var borderContainer = grid?.Parent as Border;

            if (string.IsNullOrWhiteSpace(searchText) || searchText == "Search...")
            {
                resultsPanel.Children.Clear();
                if (borderContainer != null)
                {
                    borderContainer.Visibility = Visibility.Collapsed;
                }
                return;
            }

            try
            {
                resultsPanel.Children.Clear();

                if (borderContainer != null)
                {
                    borderContainer.Visibility = Visibility.Visible;
                }

                var users = await _apiService.SearchUsersAsync(searchText);

                if (users.Count == 0)
                {
                    var noResultsText = new TextBlock
                    {
                        Text = "No users found",
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#057272")),
                        Margin = new Thickness(15),
                        FontSize = 14,
                        TextAlignment = TextAlignment.Left,
                        FontWeight = FontWeights.Medium
                    };
                    resultsPanel.Children.Add(noResultsText);
                    return;
                }

                foreach (var user in users)
                {
                    if (user.Id == _currentUserId) continue;

                    var userButton = _uiBuilder.CreateUserSearchResultButton(user, async (selectedUser) =>
                    {
                        await onUserSelected(selectedUser);
                        resultsPanel.Children.Clear();
                        if (borderContainer != null)
                        {
                            borderContainer.Visibility = Visibility.Collapsed;
                        }
                        searchTextBox.Text = "Search...";
                        searchTextBox.Foreground = Brushes.Gray;
                    });

                    resultsPanel.Children.Add(userButton);
                }
            }
            catch (Exception ex)
            {
                resultsPanel.Children.Clear();
                var errorText = new TextBlock
                {
                    Text = $"Error: {ex.Message}",
                    Foreground = Brushes.Red,
                    Margin = new Thickness(15),
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap
                };
                resultsPanel.Children.Add(errorText);

                if (borderContainer != null)
                {
                    borderContainer.Visibility = Visibility.Visible;
                }
            }
        }
    }
}