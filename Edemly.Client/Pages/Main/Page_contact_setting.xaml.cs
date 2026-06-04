#nullable disable

using Edemly.Client.Lang;
using Edemly.Client.Models;
using Edemly.Client.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Edemly.Client
{
    public partial class Page_contact_setting : Page
    {
        private Contact _contact;
        private const int MAX_CONTACTS_WITH_NOTES = 5;

        public Page_contact_setting(Contact contact)
        {
            InitializeComponent();
            _contact = contact;

            ThemeService.Instance.ThemeChanged += (themeName) => OnThemeChanged();

            ApplyThemeToPage();

            LoadInterfaceTexts();
            LoadContactData();
            _ = LoadNoteAsync();
        }

        private void OnThemeChanged()
        {
            try
            {
                ApplyThemeToPage();
                System.Diagnostics.Debug.WriteLine("[PAGE_CONTACT_SETTING] Theme changed");
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
                }

                if (AddNoteButton != null)
                {
                    AddNoteButton.Background = new SolidColorBrush(palette.Primary);
                }
                if (DeleteNoteButton != null)
                {
                    DeleteNoteButton.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69));
                }

                var contactPhotoBorder = this.FindName("ContactPhotoBorder") as Border;
                if (contactPhotoBorder != null)
                {
                    contactPhotoBorder.BorderBrush = new SolidColorBrush(palette.Secondary);
                }

                System.Diagnostics.Debug.WriteLine($"[PAGE_CONTACT_SETTING] Theme applied: {ThemeService.Instance.CurrentTheme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PAGE_CONTACT_SETTING] ApplyThemeToPage error: {ex.Message}");
            }
        }

        private void LoadInterfaceTexts()
        {
            try
            {
                this.Title = DefaultLanguage.ContactSettingsTitle;

                var notesTitle = this.FindName("NotesSectionTitle") as TextBlock;
                if (notesTitle != null)
                    notesTitle.Text = DefaultLanguage.ContactNotesTitle;

                var notesPrivate = this.FindName("NotesPrivateText") as TextBlock;
                if (notesPrivate != null)
                    notesPrivate.Text = DefaultLanguage.ContactNotesPrivate;

                AddNoteButton.Content = DefaultLanguage.ContactAddNoteButton;
                DeleteNoteButton.Content = DefaultLanguage.ContactDeleteNoteButton;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONTACT SETTING] Error loading interface texts: {ex.Message}");
            }
        }

        private void LoadContactData()
        {
            if (_contact == null)
            {
                System.Diagnostics.Debug.WriteLine("[CONTACT SETTING] Contact is null!");
                return;
            }

            NameTextBox.Text = _contact.Name ?? DefaultLanguage.ContactNameUnknown;
            EmailTextBox.Text = _contact.Email ?? DefaultLanguage.ContactEmailNotSpecified;
            PhoneTextBox.Text = string.IsNullOrEmpty(_contact.Phone) ?
                DefaultLanguage.ContactPhoneNotSpecified : _contact.Phone;

            LoadContactPhoto();
        }

        private async void LoadContactPhoto()
        {
            try
            {
                if (!string.IsNullOrEmpty(_contact.PhotoPath) &&
                    _contact.PhotoPath != "pack://application:,,,/Assets/Avatars/default-avatar.png")
                {
                    var bitmap = await App.GlobalProfilePictureCache.GetOrDownloadAsync(_contact.PhotoPath);

                    if (bitmap != null)
                    {
                        ContactPhotoBackground.ImageSource = bitmap;
                    }
                    else
                    {
                        ContactPhotoBackground.ImageSource = new BitmapImage(
                            new Uri("pack://application:,,,/Assets/Avatars/default-avatar.png", UriKind.RelativeOrAbsolute));
                    }
                }
                else
                {
                    ContactPhotoBackground.ImageSource = new BitmapImage(
                        new Uri("pack://application:,,,/Assets/Avatars/default-avatar.png", UriKind.RelativeOrAbsolute));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format(DefaultLanguage.ContactPhotoLoadError, ex.Message));
                ContactPhotoBackground.ImageSource = new BitmapImage(
                    new Uri("pack://application:,,,/Assets/Avatars/default-avatar.png", UriKind.RelativeOrAbsolute));
            }
        }

        private async System.Threading.Tasks.Task LoadNoteAsync()
        {
            try
            {
                if (App.NotesService == null)
                {
                    System.Diagnostics.Debug.WriteLine("[CONTACT SETTING] NotesService is null!");
                    MessageBox.Show(DefaultLanguage.ContactNotesServiceError,
                        DefaultLanguage.ContactErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var note = await App.NotesService.GetNoteAsync(_contact.UserId);
                _contact.Note = note ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(note))
                {
                    NewNoteTextBox.Text = note;
                    AddNoteButton.Content = DefaultLanguage.ContactUpdateNoteButton;
                    DeleteNoteButton.Visibility = Visibility.Visible;
                }
                else
                {
                    DeleteNoteButton.Visibility = Visibility.Collapsed;

                    var canAdd = await App.NotesService.CanAddNoteAsync(_contact.UserId);

                    if (!canAdd)
                    {
                        AddNoteSection.Visibility = Visibility.Collapsed;
                        MaxNotesWarning.Visibility = Visibility.Visible;
                        MaxNotesWarning.Text = string.Format(DefaultLanguage.ContactNotesLimitWarning, MAX_CONTACTS_WITH_NOTES);
                    }
                    else
                    {
                        AddNoteSection.Visibility = Visibility.Visible;
                        MaxNotesWarning.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONTACT SETTING] Load note error: {ex.Message}");
                MessageBox.Show(string.Format(DefaultLanguage.ContactLoadNoteError, ex.Message),
                    DefaultLanguage.ContactErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddNoteButton_Click(object sender, RoutedEventArgs e)
        {
            string noteText = NewNoteTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(noteText))
            {
                MessageBox.Show(DefaultLanguage.ContactEmptyNoteWarning,
                    DefaultLanguage.ContactWarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (App.NotesService == null)
                {
                    MessageBox.Show(DefaultLanguage.ContactNotesServiceError,
                        DefaultLanguage.ContactErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var canAdd = await App.NotesService.CanAddNoteAsync(_contact.UserId);

                if (!canAdd)
                {
                    MessageBox.Show(string.Format(DefaultLanguage.ContactNotesLimitReached, MAX_CONTACTS_WITH_NOTES),
                        DefaultLanguage.ContactWarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool success = await App.NotesService.SaveNoteAsync(_contact.UserId, noteText);

                if (success)
                {
                    _contact.Note = noteText;
                    AddNoteButton.Content = DefaultLanguage.ContactUpdateNoteButton;
                    DeleteNoteButton.Visibility = Visibility.Visible;
                }
                else
                {
                    MessageBox.Show(DefaultLanguage.ContactSaveNoteError,
                        DefaultLanguage.ContactErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CONTACT SETTING] Save note error: {ex.Message}");
                MessageBox.Show(string.Format(DefaultLanguage.ContactSaveNoteErrorDetails, ex.Message),
                    DefaultLanguage.ContactErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteNoteButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(DefaultLanguage.ContactDeleteConfirmMessage,
                DefaultLanguage.ContactDeleteConfirmTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (App.NotesService == null)
                    {
                        MessageBox.Show(DefaultLanguage.ContactNotesServiceError,
                            DefaultLanguage.ContactErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    bool success = await App.NotesService.DeleteNoteAsync(_contact.UserId);

                    if (success)
                    {
                        _contact.Note = string.Empty;
                        NewNoteTextBox.Text = string.Empty;
                        AddNoteButton.Content = DefaultLanguage.ContactAddNoteButton;
                        DeleteNoteButton.Visibility = Visibility.Collapsed;
                        MaxNotesWarning.Visibility = Visibility.Collapsed;
                        AddNoteSection.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        MessageBox.Show(DefaultLanguage.ContactDeleteNoteError,
                            DefaultLanguage.ContactErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CONTACT SETTING] Delete note error: {ex.Message}");
                    MessageBox.Show(string.Format(DefaultLanguage.ContactDeleteNoteErrorDetails, ex.Message),
                        DefaultLanguage.ContactErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null && NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
    }
}