// Temporary bridge during the client namespace/folder refactor.
// Remove broad global imports as modules settle into their final structure.
global using Application = System.Windows.Application;
global using Edemly.Client.Application.Localization;
global using Edemly.Client.Infrastructure.Audio;
global using Edemly.Client.Infrastructure.Caching;
global using Edemly.Client.Infrastructure.Notifications;
global using Edemly.Client.Infrastructure.Realtime;
global using Edemly.Client.Infrastructure.Storage;
global using Edemly.Contracts.Auth;
global using Edemly.Contracts.Calls;
global using Edemly.Contracts.ChatMembers;
global using Edemly.Contracts.Chats;
global using Edemly.Contracts.Messages;
global using Edemly.Contracts.Notes;
global using Edemly.Contracts.Payments;
global using Edemly.Contracts.Remindings;
global using Edemly.Contracts.Realtime;
global using Edemly.Contracts.Users;
global using Edemly.Client.Presentation.Pages.Calendar;
global using MessageBox = Edemly.Client.Presentation.Dialogs.AppMessageBox;
global using Edemly.Client.Presentation.Pages.Auth;
global using Edemly.Client.Presentation.Pages.Info;
global using Edemly.Client.Presentation.Pages.Main;
global using Edemly.Client.Presentation.Pages.Payments;
global using Edemly.Client.Presentation.Pages.Settings;
global using Edemly.Client.Presentation.Windows.Calls;
