#nullable enable

using Edemly.Client.Application.Localization;
using Edemly.Contracts.ChatMembers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Pages.Main.Helpers
{
    internal sealed record GroupSettingsMemberItemView(
        Border Container,
        Border AvatarBorder,
        TextBlock NameTextBlock,
        TextBlock DetailTextBlock);

    internal static class GroupSettingsMemberItemFactory
    {
        internal static TextBlock CreateCenteredStatusText(
            string text,
            string foregroundResourceKey = "ThemeTextSecondaryBrush")
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };

            textBlock.SetResourceReference(TextBlock.ForegroundProperty, foregroundResourceKey);
            return textBlock;
        }

        internal static GroupSettingsMemberItemView Create(ChatMemberDto member)
        {
            var container = new Border
            {
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatarBorder = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarBorder.SetResourceReference(Border.BackgroundProperty, "ThemeBorderLightBrush");

            var placeholderText = new TextBlock
            {
                Text = member.UserId.ToString().Length > 2
                    ? member.UserId.ToString()[^2..]
                    : member.UserId.ToString(),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            placeholderText.SetResourceReference(TextBlock.ForegroundProperty, "ThemePrimaryBrush");
            avatarBorder.Child = placeholderText;

            Grid.SetColumn(avatarBorder, 0);
            grid.Children.Add(avatarBorder);

            var textPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var nameText = new TextBlock
            {
                Text = string.Format(DefaultLanguage.UserIdText, member.UserId),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Left,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            nameText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextPrimaryBrush");
            textPanel.Children.Add(nameText);

            var detailText = new TextBlock
            {
                Text = DefaultLanguage.LoadingText,
                FontSize = 11,
                FontStyle = FontStyles.Italic,
                TextAlignment = TextAlignment.Left,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            detailText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextSecondaryBrush");
            textPanel.Children.Add(detailText);

            Grid.SetColumn(textPanel, 1);
            grid.Children.Add(textPanel);

            var roleBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            roleBadge.SetResourceReference(
                Border.BackgroundProperty,
                member.Role == 1 ? "ThemePrimaryBrush" : "ThemeTextSecondaryBrush");

            var roleText = new TextBlock
            {
                Text = member.Role == 1 ? DefaultLanguage.OwnerRole : DefaultLanguage.MemberRole,
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            roleText.SetResourceReference(TextBlock.ForegroundProperty, "ThemeOnPrimaryTextBrush");
            roleBadge.Child = roleText;

            Grid.SetColumn(roleBadge, 2);
            grid.Children.Add(roleBadge);

            container.Child = grid;
            container.MouseEnter += (_, _) => container.SetResourceReference(Border.BackgroundProperty, "ThemeBorderLightBrush");
            container.MouseLeave += (_, _) => container.Background = Brushes.Transparent;

            return new GroupSettingsMemberItemView(container, avatarBorder, nameText, detailText);
        }
    }
}
