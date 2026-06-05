#nullable enable

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Edemly.Client.Presentation.ViewModels.Chats
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _photoPath = string.Empty;
        private int _chatId;
        private int _userId;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string PhotoPath
        {
            get => _photoPath;
            set
            {
                _photoPath = value;
                OnPropertyChanged();
            }
        }

        public int ChatId
        {
            get => _chatId;
            set
            {
                _chatId = value;
                OnPropertyChanged();
            }
        }

        public int UserId
        {
            get => _userId;
            set
            {
                _userId = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
