#nullable enable

using Edemly.Client.Application.Users.Profile;

namespace Edemly.Client.Presentation.Pages.Settings
{
    public partial class Page_settings
    {
        private void ApplyProfileSnapshot(UserProfileSnapshot snapshot)
        {
            FirstNameTextBox.Text = snapshot.FirstName;
            LastNameTextBox.Text = snapshot.LastName;
            UsernameTextBox.Text = snapshot.Username;
            EmailTextBox.Text = snapshot.Email;
            PhoneNumberTextBox.Text = snapshot.PhoneNumber;
            AboutTextBox.Text = snapshot.Description;
        }

        private void SyncCurrentUserProfile(UserProfileSnapshot snapshot)
        {
            App.CurrentUserEmail = snapshot.Email;
            App.CurrentUsername = snapshot.Username;
            App.CurrentUserName = ProfileInputValidator.BuildDisplayName(snapshot.FirstName, snapshot.LastName, snapshot.Username);
            App.CurrentUserPhotoUrl = snapshot.PfpUrl;
            CurrentUserProfileState.Apply(snapshot);
        }
    }
}
