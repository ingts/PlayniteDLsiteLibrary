using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using DLsiteMetadata;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace DLsiteLibrary
{
    public enum AuthStatus
    {
        Ok,
        AuthRequired,
        Failed
    }

    public class DLsiteLibrarySettings : ObservableObject
    {
        private string _sId = string.Empty;

        public string sId
        {
            get => _sId;
            set => SetValue(ref _sId, value);
        }

        private bool _includeIllustrators;
        private bool _includeMusicCreators;
        private bool _includeScenarioWriters;
        private bool _includeVoiceActors;

        private bool _includeProductFormat = true;
        private bool _includeFileFormat;

        private string _pageLanguage = "English";

        [DontSerialize]
        public List<string> AvailableSearchCategory { get; } =
        [
            "All categories",
            "All ages Doujin / Indie Games",
            "All ages PC Games",
            "Adult Doujin / Indie Games",
            "Adult H Games"
        ];

        [DontSerialize]
        public List<string> AvailableLanguages { get; } =
        [
            "Japanese",
            "English",
            "Simplified Chinese",
            "Traditional Chinese",
            "Korean",
            "Spanish",
            "German",
            "French",
            "Indonesian",
            "Italian",
            "Portuguese",
            "Swedish",
            "Thai",
            "Vietnamese"
        ];

        public string PageLanguage
        {
            get => _pageLanguage;
            set => SetValue(ref _pageLanguage, value);
        }

        public bool IncludeIllustrators
        {
            get => _includeIllustrators;
            set => SetValue(ref _includeIllustrators, value);
        }

        public bool IncludeScenarioWriters
        {
            get => _includeScenarioWriters;
            set => SetValue(ref _includeScenarioWriters, value);
        }

        public bool IncludeMusicCreators
        {
            get => _includeMusicCreators;
            set => SetValue(ref _includeMusicCreators, value);
        }

        public bool IncludeVoiceActors
        {
            get => _includeVoiceActors;
            set => SetValue(ref _includeVoiceActors, value);
        }

        public bool IncludeProductFormat
        {
            get => _includeProductFormat;
            set => SetValue(ref _includeProductFormat, value);
        }

        public bool IncludeFileFormat
        {
            get => _includeFileFormat;
            set => SetValue(ref _includeFileFormat, value);
        }

        public SupportedLanguages GetSupportedLanguage()
        {
            return _pageLanguage switch
            {
                "Japanese" => SupportedLanguages.ja_JP,
                "English" => SupportedLanguages.en_US,
                "Simplified Chinese" => SupportedLanguages.zh_CN,
                "Traditional Chinese" => SupportedLanguages.zh_TW,
                "Korean" => SupportedLanguages.ko_KR,
                "Spanish" => SupportedLanguages.es_ES,
                "German" => SupportedLanguages.de_DE,
                "French" => SupportedLanguages.fr_FR,
                "Indonesian" => SupportedLanguages.id_ID,
                "Italian" => SupportedLanguages.it_IT,
                "Portuguese" => SupportedLanguages.pt_BR,
                "Swedish" => SupportedLanguages.sv_SE,
                "Thai" => SupportedLanguages.th_TH,
                "Vietnamese" => SupportedLanguages.vi_VN,
                _ => SupportedLanguages.en_US
            };
        }
    }

    public class DLsiteLibrarySettingsViewModel : ObservableObject, ISettings
    {
        private readonly DLsiteLibrary plugin;
        private DLsiteLibrarySettings editingClone { get; set; }

        private DLsiteLibrarySettings settings;
        public IPlayniteAPI PlayniteApi { get; set; }

        public AuthStatus AuthStatus => string.IsNullOrEmpty(Settings.sId) ? AuthStatus.AuthRequired : AuthStatus.Ok;

        public DLsiteLibrarySettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand<object> LoginCommand =>
            new(_ =>
            {
                using (var view = PlayniteApi.WebViews.CreateView(675, 440, Colors.Black))
                {
                    view.DeleteDomainCookies(".dlsite.com");
                    view.LoadingChanged += async (o, e) =>
                    {
                        if (view.GetCurrentAddress().Contains("mypage"))
                        {
                            var httpCookies = await Task.Run(() => view.GetCookies());

                            if (httpCookies.Any())
                            {
                                Settings.sId = httpCookies
                                    .FirstOrDefault(c => c.Name == "__DLsite_SID")?.Value;
                            }

                            view.Close();
                        }
                    };

                    view.Navigate("https://www.dlsite.com/home/login/=/skip_register/1");
                    view.OpenDialog();
                }

                OnPropertyChanged(nameof(AuthStatus));
            });

        public DLsiteLibrarySettingsViewModel(DLsiteLibrary plugin, IPlayniteAPI api)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;
            PlayniteApi = api;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<DLsiteLibrarySettings>();

            // LoadPluginSettings returns null if no saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new DLsiteLibrarySettings();
            }
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            Settings = editingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }
    }
}