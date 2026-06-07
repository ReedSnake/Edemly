#nullable enable

using Edemly.Client.Application.Users.Profile;

namespace Edemly.Client.Presentation.Pages.Settings
{
    internal sealed class SettingsProfileEditorState
    {
        public UserProfileSnapshot OriginalProfile { get; private set; } = UserProfileSnapshot.Empty;
        public string CurrentAvatarPath { get; private set; } = string.Empty;
        public bool IsInitialized { get; private set; }
        public bool HasUnsavedChanges { get; private set; }

        public bool TryBeginInitialization()
        {
            if (IsInitialized)
            {
                return false;
            }

            IsInitialized = true;
            return true;
        }

        public void Load(UserProfileSnapshot snapshot)
        {
            OriginalProfile = snapshot ?? UserProfileSnapshot.Empty;
            CurrentAvatarPath = OriginalProfile.PfpUrl;
            HasUnsavedChanges = false;
        }

        public void UpdateAvatar(string? avatarPath)
        {
            CurrentAvatarPath = avatarPath?.Trim() ?? string.Empty;
            OriginalProfile = OriginalProfile with { PfpUrl = CurrentAvatarPath };
        }

        public void SetCurrentAvatar(string? avatarPath)
        {
            CurrentAvatarPath = avatarPath?.Trim() ?? string.Empty;
        }

        public void MarkSaved(UpdateUserDto request, string? email)
        {
            OriginalProfile = UserProfileSnapshot.From(request, email);
            CurrentAvatarPath = OriginalProfile.PfpUrl;
            HasUnsavedChanges = false;
        }

        public bool UpdateHasChanges(UpdateUserDto request)
        {
            HasUnsavedChanges = !OriginalProfile.Matches(request);
            return HasUnsavedChanges;
        }
    }
}
