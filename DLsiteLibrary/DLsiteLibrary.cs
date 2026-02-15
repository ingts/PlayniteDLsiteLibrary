using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace DLsiteLibrary
{
    public class DLsiteLibrary : LibraryPlugin
    {
        private DLsiteLibrarySettingsViewModel settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("f3ae3dcc-72fc-4185-8cf2-76e5aeb277bc");

        public override string Name => "DLsite";

        // Implementing Client adds ability to open it via special menu in playnite.
        // public override LibraryClient Client { get; } = new DLsiteLibraryClient();

        private readonly Uri _boughtsUrl = new("https://www.dlsite.com/maniax/load/bought/product");

        public DLsiteLibrary(IPlayniteAPI api) : base(api)
        {
            settings = new DLsiteLibrarySettingsViewModel(this, api);
            Properties = new LibraryPluginProperties
            {
                HasSettings = true,
                CanShutdownClient = false,
            };
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            if (settings.AuthStatus == AuthStatus.AuthRequired)
            {
                PlayniteApi.Notifications.Add("dlsitelogin", "DLsite library not logged in", NotificationType.Error);
            }

            var cookieContainer = new CookieContainer();
            cookieContainer.Add(_boughtsUrl, new Cookie("__DLsite_SID", settings.Settings.sId));
            var handler = new HttpClientHandler { CookieContainer = cookieContainer };
            var httpClient = new HttpClient(handler);

            var res = httpClient.GetAsync(_boughtsUrl).Result;

            try
            {
                res.EnsureSuccessStatusCode();
            }
            catch (Exception)
            {
                PlayniteApi.Notifications.Add("dlsite_failed", "Failed to load DLsite library", NotificationType.Error);
                throw;
            }

            var boughtsResponse = Serialization.FromJson<BoughtsResponse>(res.Content.ReadAsStringAsync().Result);

            // Returns 200 even if expired
            if (boughtsResponse.Boughts.Count == 0)
            {
                settings.Settings.sId = string.Empty;
                PlayniteApi.Notifications.Add(new NotificationMessage("dlsite_expired",
                    "No products found. Session might have expired. Try logging in again.",
                    NotificationType.Error, () => OpenSettingsView()));
                return [];
            }

            return boughtsResponse.Boughts.Select(s => new GameMetadata
            {
                GameId = s,
                Source = new MetadataNameProperty("DLsite"),
                IsInstalled = false
            });
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new DLsiteLibrarySettingsView();
        }

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new MetadataProvider(settings.Settings, PlayniteApi);
        }
    }
}