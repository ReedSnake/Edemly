using Edemly.Client.Application.Services;
namespace Edemly.Client.Application.Localization
{
    public static class DefaultLanguage
    {
        private static LanguageService Lang => LanguageService.Instance;

        public static string AppTitle => Lang.GetText("app", "title", "Edemly");
        public static string LoginButton => Lang.GetText("page_login", "login_button", "Login");
        public static string RegisterButton => Lang.GetText("page_login", "register_button", "Register");
        public static string LogoutButton => Lang.GetText("app", "logout_button", "Logout");

        public static string Sending => Lang.GetText("common", "sending", "Sending...");
        public static string PleaseEnterEmail => Lang.GetText("common", "please_enter_email", "Please enter email");
        public static string PleaseEnterValidEmail => Lang.GetText("common", "please_enter_valid_email", "Please enter a valid email address");
        public static string FailedSendVerification => Lang.GetText("common", "failed_send_verification", "Failed to send verification code. Please try again.");
        public static string Yes => Lang.GetText("common", "yes", "Yes");
        public static string No => Lang.GetText("common", "no", "No");
        public static string Ok => Lang.GetText("common", "ok", "OK");
        public static string Cancel => Lang.GetText("common", "cancel", "Cancel");
        public static string Close => Lang.GetText("common", "close", "Close");
        public static string Delete => Lang.GetText("common", "delete", "Delete");
        public static string Edit => Lang.GetText("common", "edit", "Edit");
        public static string Save => Lang.GetText("common", "save", "Save");
        public static string Copy => Lang.GetText("common", "copy", "Copy");
        public static string Error => Lang.GetText("common", "error", "Error");
        public static string Warning => Lang.GetText("common", "warning", "Warning");
        public static string Success => Lang.GetText("common", "success", "Success");
        public static string Information => Lang.GetText("common", "information", "Information");
        public static string Loading => Lang.GetText("common", "loading", "Loading...");
        public static string Search => Lang.GetText("common", "search", "Search...");
        public static string NoResults => Lang.GetText("common", "no_results", "No results found");
        public static string Retry => Lang.GetText("common", "retry", "Retry");

        public static string WelcomeTitle => Lang.GetText("page_login", "welcome_title", "Hello!");
        public static string WelcomeSubtitle => Lang.GetText("page_login", "welcome_subtitle", "Welcome back");
        public static string LoginEmailLabel => Lang.GetText("page_login", "email_label", "Email");
        public static string PasswordLabel => Lang.GetText("page_login", "password_label", "Password");
        public static string NoAccount => Lang.GetText("page_login", "no_account", "No account? ");
        public static string SignUp => Lang.GetText("page_login", "sign_up", "Sign up");
        public static string RememberMe => Lang.GetText("page_login", "remember_me", "Remember me");

        public static string UsernamePlaceholder => Lang.GetText("page_login", "username_placeholder", "Enter username");
        public static string PasswordPlaceholder => Lang.GetText("page_login", "password_placeholder", "Enter password");
        public static string EmailPlaceholder => Lang.GetText("page_login", "email_placeholder", "Enter email");

        public static string LoginSuccess => Lang.GetText("page_login", "login_success", "Successfully logged in!");
        public static string LoginFailed => Lang.GetText("page_login", "login_failed", "Login failed. Please check your credentials.");
        public static string RegistrationSuccess => Lang.GetText("page_registration", "registration_success", "Registration successful!");
        public static string ErrorOccurred => Lang.GetText("app", "error_occurred", "An error occurred. Please try again.");

        public static string InstallTitle => Lang.GetText("page_install", "title", "Edemly Installation");
        public static string InstallDescription => Lang.GetText("page_install", "description", "Configure initial settings before using Edemly. You can change these later in Settings.");
        public static string LanguageLabel => Lang.GetText("page_install", "language_label", "Language");
        public static string LanguageDesc => Lang.GetText("page_install", "language_desc", "Choose application language");
        public static string CompanyLabel => Lang.GetText("page_install", "company_label", "Company (optional)");
        public static string CompanyDesc => Lang.GetText("page_install", "company_desc", "Select a company for tenant installs or leave as Personal for single-instance use");
        public static string DesktopShortcutLabel => Lang.GetText("page_install", "desktop_shortcut_label", "Create desktop shortcut");
        public static string NoteInitial => Lang.GetText("page_install", "note_initial", "Initial setup. Companies list is loaded from server. \"Personal\" is always the first option.");
        public static string CancelButton => Lang.GetText("page_install", "cancel_button", "CANCEL");
        public static string ContinueButton => Lang.GetText("page_install", "continue_button", "CONTINUE");
        public static string PersonalLabel => Lang.GetText("page_install", "personal_label", "Personal");
        public static string LoadingCompanies => Lang.GetText("page_install", "loading_companies", "Loading companies...");
        public static string ServerNotProvided => Lang.GetText("page_install", "server_not_provided", "Server URL not provided.");
        public static string CompaniesLoadFailed => Lang.GetText("page_install", "companies_load_failed", "Could not load companies list. Using Personal mode.");
        public static string SelectCompanyNote => Lang.GetText("page_install", "select_company_note", "Select a company or keep Personal.");
        public static string CompaniesErrorFallback => Lang.GetText("page_install", "companies_error_fallback", "Error loading companies. Using Personal mode.");
        public static string ShortcutCreateFailed => Lang.GetText("page_install", "shortcut_create_failed", "Failed to create desktop shortcut.");

        public static string SettingsTitle => Lang.GetText("page_settings", "settings_title", "Settings");
        public static string SelectProfilePhotoTitle => Lang.GetText("page_settings", "select_profile_photo", "Select profile photo");
        public static string Uploading => Lang.GetText("page_settings", "uploading", "Uploading...");
        public static string ChangePhoto => Lang.GetText("page_settings", "change_photo", "Change Photo");
        public static string PhotoUploadFailed => Lang.GetText("page_settings", "photo_upload_failed", "Failed to upload photo: {0}");
        public static string PhotoUploadedButUpdateFailed => Lang.GetText("page_settings", "photo_uploaded_but_update_failed", "Photo uploaded but failed to update profile on server. It will be applied locally.");
        public static string ProfilePhotoUpdated => Lang.GetText("page_settings", "profile_photo_updated", "Profile photo updated");
        public static string SettingsSaved => Lang.GetText("page_settings", "settings_saved", "Settings saved");
        public static string FailedSaveUserSettings => Lang.GetText("page_settings", "failed_save_user_settings", "Failed to save user settings");
        public static string ErrorSavingSettings => Lang.GetText("page_settings", "error_saving_settings", "Error saving settings: {0}");
        public static string PleaseEnterValidPhone => Lang.GetText("page_settings", "please_enter_valid_phone", "Please enter a valid phone number");
        public static string SuccessTitle => Lang.GetText("page_settings", "success_title", "Success");
        public static string ErrorTitle => Lang.GetText("page_settings", "error_title", "Error");
        public static string WarningTitle => Lang.GetText("page_settings", "warning_title", "Warning");
        public static string SaveButton => Lang.GetText("page_settings", "save_button", "Save");

        public static string LanguageEnglishName => Lang.GetText("page_settings", "lang_english", "English");
        public static string LanguageUkrainianName => Lang.GetText("page_settings", "lang_ukrainian", "Ukrainian");
        public static string SelectLanguageLabel => Lang.GetText("page_settings", "select_language_label", "Select language:");
        public static string ThemeSettings => Lang.GetText("page_settings", "theme_settings", "Theme settings:");
        public static string ThemeColor => Lang.GetText("page_settings", "theme_color", "Theme color");

        public static string RegistrationTitleLine1 => Lang.GetText("page_registration", "title_line1", "Create");
        public static string RegistrationTitleLine2 => Lang.GetText("page_registration", "title_line2", "Your Account");
        public static string UserNameLabel => Lang.GetText("page_registration", "user_name_label", "User Name");
        public static string RegistrationEmailLabel => Lang.GetText("page_registration", "email_label", "Email");
        public static string TermsLine1 => Lang.GetText("page_registration", "terms_line1", "By signing up, you agree to our");
        public static string TermsLine2 => Lang.GetText("page_registration", "terms_line2", "Terms of Service and Privacy Policy");
        public static string SignUpButton => Lang.GetText("page_registration", "sign_up_button", "Sign Up");
        public static string AlreadyHaveAccount => Lang.GetText("page_registration", "already_have_account", "Already have an account? ");
        public static string SignIn => Lang.GetText("page_registration", "sign_in", "Sign in");

        public static string PoliciesTitle => Lang.GetText("page_registration", "policies_title", "Terms of Service and Privacy Policy");
        public static string PoliciesClose => Lang.GetText("page_registration", "policies_close", "Close");
        public static string PoliciesAccept => Lang.GetText("page_registration", "policies_accept", "Accept");
        public static string PoliciesContent => Lang.GetText("page_registration", "policies_content", "");

        public static string PleaseEnterUsername => Lang.GetText("page_registration", "please_enter_username", "Please enter your username");
        public static string UsernameLength => Lang.GetText("page_registration", "username_length", "Username must be between 3 and 50 characters");
        public static string UsernameInvalid => Lang.GetText("page_registration", "username_invalid", "Username can only contain letters, numbers, underscores and hyphens");
        public static string PleaseAgreeTerms => Lang.GetText("page_registration", "please_agree_terms", "Please agree to the Terms of Service and Privacy Policy");

        public static string PleaseEnterValidCode => Lang.GetText("page_verification", "please_enter_valid_code", "Please enter a valid 6-digit code");
        public static string Verifying => Lang.GetText("page_verification", "verifying", "Verifying...");
        public static string RegistrationFailedMessage => Lang.GetText("page_verification", "registration_failed", "Registration failed. User might already exist or code is invalid.");
        public static string LoginFailedMessage => Lang.GetText("page_verification", "login_failed_message", "Login failed. Invalid code or user not found.");
        public static string VerificationResent => Lang.GetText("page_verification", "verification_resent", "Verification code resent");
        public static string FailedResendCode => Lang.GetText("page_verification", "failed_resend_code", "Failed to resend code");
        public static string VerifyButton => Lang.GetText("page_verification", "verify_button", "Verify");

        public static string VerificationTitle => Lang.GetText("page_verification", "title", "Verification Code");
        public static string VerificationDescription => Lang.GetText("page_verification", "description", "Please enter the 6-digit verification code that was sent to your email address.");
        public static string ResendPrompt => Lang.GetText("page_verification", "resend_prompt", "Didn't receive the code?");
        public static string ResendAction => Lang.GetText("page_verification", "resend_action", "Resend");
        public static string BackToLoginText => Lang.GetText("page_verification", "back_to_login", "Back to");
        public static string BackToLoginAction => Lang.GetText("page_verification", "back_to_login_action", "Login");

        public static string AboutTitle => Lang.GetText("page_about", "title", "About Edemly");
        public static string AboutGreeting => Lang.GetText("page_about", "greeting", "Hi, dear user!");
        public static string AboutWelcome => Lang.GetText("page_about", "welcome", "We are the Edemly team, and we are happy to welcome you to our messenger!");
        public static string AboutGoal => Lang.GetText("page_about", "goal", "Our goal is to create a space where communication and planning complement each other organically. We believe that modern people need not just another messenger, but a tool that helps them not only stay connected, but also manage their time effectively.");
        public static string AboutFaqTitle => Lang.GetText("page_about", "faq_title", "FAQs:");
        public static string AboutFaqContactQuestion => Lang.GetText("page_about", "faq_contact_question", "How do I add a new contact?");
        public static string AboutFaqContactAnswer => Lang.GetText("page_about", "faq_contact_answer", "Use the search function in the main menu. Enter the user's name or email address, and you can start chatting.");
        public static string AboutFaqThemeQuestion => Lang.GetText("page_about", "faq_theme_question", "How do I customize the theme?");
        public static string AboutFaqThemeAnswer => Lang.GetText("page_about", "faq_theme_answer", "Go to the main menu and select 'Settings.' There you will find various design options to suit your taste.");
        public static string AboutFaqSchedulerQuestion => Lang.GetText("page_about", "faq_scheduler_question", "How does the task scheduler work?");
        public static string AboutFaqSchedulerAnswer => Lang.GetText("page_about", "faq_scheduler_answer", "You can create tasks and set deadlines. All tasks are synchronized with your calendar so you can see the full picture of your activities.");
        public static string AboutFaqSupportQuestion => Lang.GetText("page_about", "faq_support_question", "How do I contact support?");
        public static string AboutFaqSupportAnswer => Lang.GetText("page_about", "faq_support_answer", "If you have any questions or problems, please contact us using the form: ");
        public static string AboutFaqSupportLink => Lang.GetText("page_about", "faq_support_link", "Click here");
        public static string AboutClosing => Lang.GetText("page_about", "closing", "We create Edemly with you in mind. Every feature is designed to make your life easier and more organized.");
        public static string AboutThanks => Lang.GetText("page_about", "thanks", "Thank you for choosing us!");

        public static string PremiumTitle => Lang.GetText("page_premium", "title", "Edemly Premium");
        public static string PremiumMainTitle => Lang.GetText("page_premium", "main_title", "Edemly Premium — More opportunities for you!");
        public static string PremiumDescription => Lang.GetText("page_premium", "description", "Want to use Edemly to its full potential? Upgrade to Premium and get advanced planning and organization features!");
        public static string PremiumWhatIncluded => Lang.GetText("page_premium", "what_included", "What is included in Premium?");
        public static string PremiumFeature1Title => Lang.GetText("page_premium", "feature1_title", "📝 Unlimited notes");
        public static string PremiumFeature1Desc => Lang.GetText("page_premium", "feature1_desc", "The free version offers a limited number of notes. With Premium, you can create as many notes as you need — for work, study, personal matters, and creative ideas.");
        public static string PremiumFeature2Title => Lang.GetText("page_premium", "feature2_title", "⏰ Advanced reminder system");
        public static string PremiumFeature2Desc => Lang.GetText("page_premium", "feature2_desc", "Create an unlimited number of reminders with flexible settings.");
        public static string PremiumFeature3Title => Lang.GetText("page_premium", "feature3_title", "🎨 Exclusive themes");
        public static string PremiumFeature3Desc => Lang.GetText("page_premium", "feature3_desc", "Access premium themes and fine-tune the interface to suit your style.");
        public static string PremiumFeature4Title => Lang.GetText("page_premium", "feature4_title", "⚡ Priority support");
        public static string PremiumFeature4Desc => Lang.GetText("page_premium", "feature4_desc", "Your questions will be handled first — we are always ready to help our users.");
        public static string PremiumNote => Lang.GetText("page_premium", "note", "*After your subscription ends, you will retain an unlimited number of notes.");
        public static string PremiumMonthlyButton => Lang.GetText("page_premium", "monthly_button", "SUBSCRIBE FOR 2$/MONTH");
        public static string PremiumYearlyButton => Lang.GetText("page_premium", "yearly_button", "SUBSCRIBE FOR 10$/YEAR");
        public static string PremiumPaymentError => Lang.GetText("page_premium", "payment_error", "Payment error");
        public static string PremiumApiError => Lang.GetText("page_premium", "api_error", "API service not available");
        public static string PremiumPaymentFailed => Lang.GetText("page_premium", "payment_failed", "Failed to start payment");
        public static string PremiumOpenPageError => Lang.GetText("page_premium", "open_page_error", "Failed to open payment page: {0}");

        public static string ContactSettingsTitle => Lang.GetText("page_contact_settings", "title", "Contact Settings");
        public static string ContactPhoneNotSpecified => Lang.GetText("page_contact_settings", "phone_not_specified", "Not specified");
        public static string ContactEmailNotSpecified => Lang.GetText("page_contact_settings", "email_not_specified", "No email");
        public static string ContactNameUnknown => Lang.GetText("page_contact_settings", "name_unknown", "Unknown");
        public static string ContactNotesTitle => Lang.GetText("page_contact_settings", "notes_section_title", "Note");
        public static string ContactNotesPrivate => Lang.GetText("page_contact_settings", "notes_private_info", "*only you can see this information");
        public static string ContactAddNoteButton => Lang.GetText("page_contact_settings", "add_note_button", "Add Note");
        public static string ContactUpdateNoteButton => Lang.GetText("page_contact_settings", "update_note_button", "Update Note");
        public static string ContactDeleteNoteButton => Lang.GetText("page_contact_settings", "delete_note_button", "Delete");
        public static string ContactEmptyNoteWarning => Lang.GetText("page_contact_settings", "empty_note_warning", "Please enter a note");
        public static string ContactNotesLimitReached => Lang.GetText("page_contact_settings", "notes_limit_reached", "Maximum {0} contacts with notes reached. Delete a note from another contact first.");
        public static string ContactNotesLimitWarning => Lang.GetText("page_contact_settings", "notes_limit_warning", "Maximum {0} contacts with notes reached. Delete a note from another contact to add here.");
        public static string ContactDeleteConfirmTitle => Lang.GetText("page_contact_settings", "delete_confirm_title", "Confirm Delete");
        public static string ContactDeleteConfirmMessage => Lang.GetText("page_contact_settings", "delete_confirm_message", "Delete this note?");
        public static string ContactErrorTitle => Lang.GetText("page_contact_settings", "error_title", "Error");
        public static string ContactWarningTitle => Lang.GetText("page_contact_settings", "warning_title", "Warning");
        public static string ContactSuccessTitle => Lang.GetText("page_contact_settings", "success_title", "Success");
        public static string ContactNotesServiceError => Lang.GetText("page_contact_settings", "notes_service_error", "Notes service not initialized");
        public static string ContactLoadNoteError => Lang.GetText("page_contact_settings", "load_note_error", "Failed to load note: {0}");
        public static string ContactSaveNoteError => Lang.GetText("page_contact_settings", "save_note_error", "Failed to save note");
        public static string ContactSaveNoteErrorDetails => Lang.GetText("page_contact_settings", "save_note_error_details", "Failed to save note: {0}");
        public static string ContactDeleteNoteError => Lang.GetText("page_contact_settings", "delete_note_error", "Failed to delete note");
        public static string ContactDeleteNoteErrorDetails => Lang.GetText("page_contact_settings", "delete_note_error_details", "Failed to delete note: {0}");
        public static string ContactPhotoLoadError => Lang.GetText("page_contact_settings", "photo_load_error", "Error loading photo: {0}");

        public static string GroupIcon => Lang.GetText("page_group_settings", "group_icon", "Group Icon");
        public static string ChangeIcon => Lang.GetText("page_group_settings", "change_icon", "📷 Change Icon");
        public static string GroupName => Lang.GetText("page_group_settings", "group_name", "Group Name");
        public static string Characters1To50 => Lang.GetText("page_group_settings", "characters_1_50", "1-50 characters");
        public static string GroupDescription => Lang.GetText("page_group_settings", "group_description", "Group Description");
        public static string Characters0To200 => Lang.GetText("page_group_settings", "characters_0_200", "0-200 characters");
        public static string GroupMembers => Lang.GetText("page_group_settings", "group_members", "Group Members");
        public static string ClickMemberForOptions => Lang.GetText("page_group_settings", "click_member_for_options", "Click on member to see options");
        public static string LoadingMembers => Lang.GetText("page_group_settings", "loading_members", "Loading members...");
        public static string NoMembers => Lang.GetText("page_group_settings", "no_members", "No members in this group");
        public static string TotalMembers => Lang.GetText("page_group_settings", "total_members", "Total members: {0}");
        public static string FailedLoadMembers => Lang.GetText("page_group_settings", "failed_load_members", "Failed to load members");
        public static string SaveChanges => Lang.GetText("page_group_settings", "save_changes", "💾 Save Changes");
        public static string SaveChangesButton => Lang.GetText("page_group_settings", "save_changes_button", "Save Changes");
        public static string PermissionDenied => Lang.GetText("page_group_settings", "permission_denied", "Permission Denied");
        public static string OwnerOnlyChangeIcon => Lang.GetText("page_group_settings", "owner_only_change_icon", "Only group owner can change the icon");
        public static string OwnerOnlyChangeSettings => Lang.GetText("page_group_settings", "owner_only_change_settings", "Only group owner can change group settings");
        public static string GroupNameEmpty => Lang.GetText("page_group_settings", "group_name_empty", "Group name cannot be empty");
        public static string SelectGroupIcon => Lang.GetText("page_group_settings", "select_group_icon", "Select group icon");
        public static string IconUploadFailed => Lang.GetText("page_group_settings", "icon_upload_failed", "Failed to upload icon: {0}");
        public static string GroupSettingsUpdated => Lang.GetText("page_group_settings", "group_settings_updated", "Group settings updated successfully!");
        public static string FailedUpdate => Lang.GetText("page_group_settings", "failed_update", "Failed to update: {0}");
        public static string ErrorText => Lang.GetText("page_group_settings", "error", "Error: {0}");
        public static string OwnerRole => Lang.GetText("page_group_settings", "owner", "Owner");
        public static string MemberRole => Lang.GetText("page_group_settings", "member", "Member");
        public static string UserIdText => Lang.GetText("page_group_settings", "user_id", "User #{0}");
        public static string LoadingText => Lang.GetText("page_group_settings", "loading", "Loading...");

        public static string CalendarTitle => Lang.GetText("page_calendar", "calendar_title", "Calendar");
        public static string TasksHeader => Lang.GetText("page_calendar", "tasks_header", "TASKS");
        public static string TodaysTasks => Lang.GetText("page_calendar", "todays_tasks", "TODAY'S TASKS");
        public static string TasksForDate => Lang.GetText("page_calendar", "tasks_for_date", "TASKS FOR {0}");
        public static string PastTasksFor => Lang.GetText("page_calendar", "past_tasks_for", "TASKS FOR {0} (PAST)");

        public static string FilterAll => Lang.GetText("page_calendar", "filter_all", "All");
        public static string FilterActive => Lang.GetText("page_calendar", "filter_active", "Active");
        public static string FilterCompleted => Lang.GetText("page_calendar", "filter_completed", "Completed");
        public static string FilterUpcoming => Lang.GetText("page_calendar", "filter_upcoming", "Upcoming");
        public static string ShowLabel => Lang.GetText("page_calendar", "show_label", "Show:");

        public static string NoTasks => Lang.GetText("page_calendar", "no_tasks", "No tasks");
        public static string NoUpcomingTasks => Lang.GetText("page_calendar", "no_upcoming_tasks", "No upcoming tasks");
        public static string Today => Lang.GetText("page_calendar", "today", "Today");

        public static string CategoryImportant => Lang.GetText("page_calendar", "category_important", "Important");
        public static string CategoryWork => Lang.GetText("page_calendar", "category_work", "Work");
        public static string CategoryPersonal => Lang.GetText("page_calendar", "category_personal", "Personal");
        public static string CategorySports => Lang.GetText("page_calendar", "category_sports", "Sports");
        public static string CategoryStudy => Lang.GetText("page_calendar", "category_study", "Study");
        public static string CategoryEntertainment => Lang.GetText("page_calendar", "category_entertainment", "Entertainment");

        public static string CategoryImportantDesc => Lang.GetText("page_calendar", "category_important_desc", "Priority tasks");
        public static string CategoryWorkDesc => Lang.GetText("page_calendar", "category_work_desc", "Work tasks");
        public static string CategoryPersonalDesc => Lang.GetText("page_calendar", "category_personal_desc", "Personal matters");
        public static string CategorySportsDesc => Lang.GetText("page_calendar", "category_sports_desc", "Training, activity");
        public static string CategoryStudyDesc => Lang.GetText("page_calendar", "category_study_desc", "Learning tasks");
        public static string CategoryEntertainmentDesc => Lang.GetText("page_calendar", "category_entertainment_desc", "Rest, hobbies");

        public static string AddTaskButton => Lang.GetText("page_calendar", "add_task_button", "Add Task");
        public static string TodayButton => Lang.GetText("page_calendar", "today_button", "TODAY");
        public static string PrevMonthButton => Lang.GetText("page_calendar", "prev_month_button", "←");
        public static string NextMonthButton => Lang.GetText("page_calendar", "next_month_button", "→");
        public static string SaveTaskButton => Lang.GetText("page_calendar", "save_button", "SAVE");
        public static string CancelTaskButton => Lang.GetText("page_calendar", "cancel_button", "CANCEL");

        public static string NewTaskTitle => Lang.GetText("page_calendar", "new_task_title", "NEW TASK");
        public static string EditTaskPanelTitle => Lang.GetText("page_calendar", "edit_task_panel_title", "EDIT TASK");
        public static string AddTaskTitle => Lang.GetText("page_calendar", "add_task_title", "Add New Task");
        public static string EditTaskTitle => Lang.GetText("page_calendar", "edit_task_title", "Edit Task");
        public static string TaskNameLabel => Lang.GetText("page_calendar", "task_name_label", "Task name:");
        public static string TaskDescriptionLabel => Lang.GetText("page_calendar", "task_description_label", "Description (optional):");
        public static string TimeOptionalLabel => Lang.GetText("page_calendar", "time_optional_label", "Time (optional):");
        public static string SetTimeCheckbox => Lang.GetText("page_calendar", "set_time_checkbox", "Set time");
        public static string TimeFormatHint => Lang.GetText("page_calendar", "time_format_hint", "(24h format)");
        public static string CategoryColorLabel => Lang.GetText("page_calendar", "category_color_label", "Category (color):");
        public static string SelectTimeLabel => Lang.GetText("page_calendar", "select_time_label", "Select time");
        public static string SelectColorLabel => Lang.GetText("page_calendar", "select_color_label", "Select color");
        public static string TimeTooltip => Lang.GetText("page_calendar", "time_tooltip", "Enter time in 24-hour format (00:00 - 23:59)");

        public static string EnterTaskName => Lang.GetText("page_calendar", "enter_task_name", "Enter task name!");
        public static string EnterTimeLabel => Lang.GetText("page_calendar", "enter_time_label", "Please enter time!");
        public static string InvalidTimeFormat => Lang.GetText("page_calendar", "invalid_time_format", "Please enter time in format HH:mm (00:00 - 23:59)");
        public static string CannotAddPastDate => Lang.GetText("page_calendar", "cannot_add_past_date", "Cannot add tasks to past dates! Please select today or a future date.");
        public static string CannotEditPastDate => Lang.GetText("page_calendar", "cannot_edit_past_date", "Cannot edit tasks for past dates.");
        public static string CannotDuplicatePastDate => Lang.GetText("page_calendar", "cannot_duplicate_past_date", "Cannot duplicate tasks to past dates! Please select today or a future date.");

        public static string ContextMenuEdit => Lang.GetText("page_calendar", "context_menu_edit", "✎ Edit");
        public static string ContextMenuView => Lang.GetText("page_calendar", "context_menu_view", "👁 View");
        public static string ContextMenuDuplicate => Lang.GetText("page_calendar", "context_menu_duplicate", "⎘ Duplicate");
        public static string ContextMenuDelete => Lang.GetText("page_calendar", "context_menu_delete", "🗑️ Delete");

        public static string DuplicateTaskTitle => Lang.GetText("page_calendar", "duplicate_task_title", "Duplicate Task");
        public static string DuplicateTargetDate => Lang.GetText("page_calendar", "duplicate_target_date", "Choose target date (or pick repeating):");
        public static string DuplicateOnce => Lang.GetText("page_calendar", "duplicate_once", "Once");
        public static string DuplicateDaily => Lang.GetText("page_calendar", "duplicate_daily", "Daily");
        public static string DuplicateWeekly => Lang.GetText("page_calendar", "duplicate_weekly", "Weekly");
        public static string DuplicateMonthly => Lang.GetText("page_calendar", "duplicate_monthly", "Monthly");

        public static string DeleteTaskConfirm => Lang.GetText("page_calendar", "delete_task_confirm", "Delete this task?");
        public static string Confirm => Lang.GetText("page_calendar", "confirm", "Confirm");
        public static string InvalidDate => Lang.GetText("page_calendar", "invalid_date", "Invalid Date");
        public static string PastTask => Lang.GetText("page_calendar", "past_task", "Past Task");
        public static string PastTaskMessage => Lang.GetText("page_calendar", "past_task_message", "Cannot edit tasks in the past. You can only view or delete them.");
        public static string InformationTitle => Lang.GetText("page_calendar", "information_title", "Information");

        public static string DaySun => Lang.GetText("page_calendar", "day_sun", "Sun");
        public static string DayMon => Lang.GetText("page_calendar", "day_mon", "Mon");
        public static string DayTue => Lang.GetText("page_calendar", "day_tue", "Tue");
        public static string DayWed => Lang.GetText("page_calendar", "day_wed", "Wed");
        public static string DayThu => Lang.GetText("page_calendar", "day_thu", "Thu");
        public static string DayFri => Lang.GetText("page_calendar", "day_fri", "Fri");
        public static string DaySat => Lang.GetText("page_calendar", "day_sat", "Sat");

        public static string DeleteRepeatingTask => Lang.GetText("page_calendar", "delete_repeating_task", "Delete Repeating Task");
        public static string DeleteRepeatingTaskDescription => Lang.GetText("page_calendar", "delete_repeating_task_description", "This task is part of a repeating series. What would you like to delete?");
        public static string DeleteOnlyThis => Lang.GetText("page_calendar", "delete_only_this", "Delete only this task");
        public static string DeleteAllFuture => Lang.GetText("page_calendar", "delete_all_future", "Delete this and all future tasks");

        public static string VoiceMessage => Lang.GetText("messages", "voice_message", "Voice Message");
        public static string Photo => Lang.GetText("messages", "photo", "Photo");
        public static string File => Lang.GetText("messages", "file", "File");
        public static string You => Lang.GetText("messages", "you", "You");
        public static string ClickToOpen => Lang.GetText("messages", "click_to_open", "Click to open");
        public static string CopyMessage => Lang.GetText("messages", "copy", "📝 Copy");
        public static string EditMessage => Lang.GetText("messages", "edit", "✎ Edit");
        public static string DeleteMessage => Lang.GetText("messages", "delete", "✖ Delete");
        public static string EditMessageTitle => Lang.GetText("messages", "edit_title", "Edit Message");
        public static string EditMessageLabel => Lang.GetText("messages", "edit_label", "Edit your message:");
        public static string MessageCannotBeEmpty => Lang.GetText("messages", "cannot_be_empty", "Message cannot be empty");
        public static string FailedUpdateMessage => Lang.GetText("messages", "failed_update", "Failed to update message");
        public static string FailedDeleteMessage => Lang.GetText("messages", "failed_delete", "Failed to delete message");
        public static string ConfirmDeleteMessage => Lang.GetText("messages", "confirm_delete", "Are you sure you want to delete this message?");
        public static string FailedSendMessage => Lang.GetText("messages", "failed_send", "Failed to send message");
        public static string TypeMessage => Lang.GetText("messages", "type_message", "Type a message...");
        public static string Online => Lang.GetText("messages", "online", "Online");
        public static string Offline => Lang.GetText("messages", "offline", "Offline");
        public static string LastSeen => Lang.GetText("messages", "last_seen", "Last seen {0}");
        public static string SelectChat => Lang.GetText("messages", "select_chat", "Select a chat to start messaging");

        public static string MyPlanner => Lang.GetText("page_main", "my_planner", "My Planner");
        public static string Contacts => Lang.GetText("page_main", "contacts", "Contacts");
        public static string Chats => Lang.GetText("page_main", "chats", "Chats");
        public static string Settings => Lang.GetText("page_main", "settings", "Settings");
        public static string Profile => Lang.GetText("page_main", "profile", "Profile");
        public static string AboutApp => Lang.GetText("page_main", "about_app", "About App");
        public static string Logout => Lang.GetText("page_main", "logout", "Logout");
        public static string LogoutConfirm => Lang.GetText("page_main", "logout_confirm", "Are you sure you want to logout?");
        public static string LogoutError => Lang.GetText("page_main", "logout_error", "Logout error: {0}");
        public static string Premium => Lang.GetText("page_main", "premium", "Premium");
        public static string NewGroup => Lang.GetText("page_main", "new_group", "New Group");
        public static string CreateGroup => Lang.GetText("page_main", "create_group", "Create Group");
        public static string CreateNewGroup => Lang.GetText("page_main", "create_new_group", "Create New Group");
        public static string GroupNameLabel => Lang.GetText("page_main", "group_name", "Group Name");
        public static string GroupNamePlaceholder => Lang.GetText("page_main", "group_name_placeholder", "Enter group name");
        public static string AddParticipants => Lang.GetText("page_main", "add_participants", "Add Participants");
        public static string ParticipantsSelected => Lang.GetText("page_main", "participants_selected", "({0} selected)");
        public static string SelectMembers => Lang.GetText("page_main", "select_members", "Select members");
        public static string CreateGroupButton => Lang.GetText("page_main", "create_group_button", "Create");
        public static string GroupCreated => Lang.GetText("page_main", "group_created", "Group created successfully!");
        public static string FailedCreateGroup => Lang.GetText("page_main", "failed_create_group", "Failed to create group");
        public static string ContactInfo => Lang.GetText("page_main", "contact_info", "Contact Information");
        public static string GroupInfo => Lang.GetText("page_main", "group_info", "Group Info");
        public static string StartCall => Lang.GetText("page_main", "start_call", "Start Call");
        public static string AttachFile => Lang.GetText("page_main", "attach_file", "Attach File");
        public static string SendVoice => Lang.GetText("page_main", "send_voice", "Send Voice");
        public static string SendSticker => Lang.GetText("page_main", "send_sticker", "Send Sticker");
        public static string Menu => Lang.GetText("page_main", "menu", "Menu");
        public static string MenuSettings => Lang.GetText("page_main", "menu_settings", "⚙️ Settings");
        public static string MenuPremium => Lang.GetText("page_main", "menu_premium", "👑 Premium");
        public static string MenuAbout => Lang.GetText("page_main", "menu_about", "❓ About the app");
        public static string MenuLogout => Lang.GetText("page_main", "menu_logout", "🚪 Logout");
        public static string SearchResults => Lang.GetText("page_main", "search_results", "Search Results");
        public static string SearchPlaceholder => Lang.GetText("page_main", "search_placeholder", "Search...");
        public static string Stickers => Lang.GetText("page_main", "stickers", "Stickers");
        public static string NameLabel => Lang.GetText("page_main", "name_label", "Name");
        public static string EmailLabel => Lang.GetText("page_main", "email_label", "Email");
        public static string PhoneLabel => Lang.GetText("page_main", "phone_label", "Phone");
        public static string PersonalNotes => Lang.GetText("page_main", "personal_notes", "Personal Notes");
        public static string NotesPrivate => Lang.GetText("page_main", "notes_private", "*only you can see this information");
        public static string NoNotes => Lang.GetText("page_main", "no_notes", "No notes added yet");
        public static string DescriptionLabel => Lang.GetText("page_main", "description_label", "Description");
        public static string NoDescription => Lang.GetText("page_main", "no_description", "No description");
        public static string MembersLabel => Lang.GetText("page_main", "members_label", "Members");
        public static string GroupSettings => Lang.GetText("page_main", "group_settings", "Group Settings");
        public static string OwnerBadge => Lang.GetText("page_main", "owner_badge", "Owner");
        public static string OnlineStatus => Lang.GetText("page_main", "online_status", "Online");
        public static string OfflineStatus => Lang.GetText("page_main", "offline_status", "Offline");
        public static string CallTooltip => Lang.GetText("page_main", "call_tooltip", "Start call");
        public static string CreateGroupTooltip => Lang.GetText("page_main", "create_group_tooltip", "Create Group");

        public static string IncomingCall => Lang.GetText("call", "incoming_call", "Incoming Call");
        public static string OutgoingCall => Lang.GetText("call", "outgoing_call", "Outgoing Call");
        public static string CallEnded => Lang.GetText("call", "call_ended", "Call Ended");
        public static string Calling => Lang.GetText("call", "calling", "Calling...");
        public static string Connecting => Lang.GetText("call", "connecting", "Connecting...");
        public static string Accept => Lang.GetText("call", "accept", "Accept");
        public static string Decline => Lang.GetText("call", "decline", "Decline");
        public static string EndCall => Lang.GetText("call", "end_call", "End Call");
        public static string Mute => Lang.GetText("call", "mute", "Mute");
        public static string Unmute => Lang.GetText("call", "unmute", "Unmute");
        public static string CallDuration => Lang.GetText("call", "duration", "Duration: {0}");
        public static string CallFailed => Lang.GetText("call", "call_failed", "Call failed");
        public static string NoActiveCall => Lang.GetText("call", "no_active_call", "No active call");

        public static string Connected => Lang.GetText("connection", "connected", "Connected");
        public static string Disconnected => Lang.GetText("connection", "disconnected", "Disconnected");
        public static string Reconnecting => Lang.GetText("connection", "reconnecting", "Reconnecting...");
        public static string ConnectionLost => Lang.GetText("connection", "connection_lost", "Connection lost");

        public static string Validation => Lang.GetText("validation", "validation", "Validation");
        public static string Required => Lang.GetText("validation", "required", "This field is required");
        public static string InvalidFormat => Lang.GetText("validation", "invalid_format", "Invalid format");

        public static string SelectFile => Lang.GetText("files", "select_file", "Select File");
        public static string SelectImage => Lang.GetText("files", "select_image", "Select Image");
        public static string AllFiles => Lang.GetText("files", "all_files", "All Files");
        public static string Images => Lang.GetText("files", "images", "Images");
        public static string Documents => Lang.GetText("files", "documents", "Documents");
        public static string UploadFailed => Lang.GetText("files", "upload_failed", "Failed to upload file: {0}");
        public static string DownloadFailed => Lang.GetText("files", "download_failed", "Failed to download file: {0}");

        public static string SearchUsers => Lang.GetText("search", "search_users", "Search users...");
        public static string NoUsersFound => Lang.GetText("search", "no_users_found", "No users found");
        public static string StartTypingToSearch => Lang.GetText("search", "start_typing", "Start typing to search");

        public static string Yesterday => Lang.GetText("datetime", "yesterday", "Yesterday");
        public static string JustNow => Lang.GetText("datetime", "just_now", "Just now");
        public static string MinutesAgo => Lang.GetText("datetime", "minutes_ago", "{0} minutes ago");
        public static string HoursAgo => Lang.GetText("datetime", "hours_ago", "{0} hours ago");
        public static string DaysAgo => Lang.GetText("datetime", "days_ago", "{0} days ago");
    }
}