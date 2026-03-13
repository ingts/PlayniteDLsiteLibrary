using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace DLsiteLibrary;

public class InstallController : Playnite.SDK.Plugins.InstallController
{
    private static readonly ILogger logger = LogManager.GetLogger();
    private readonly DLsiteLibrarySettings _settings;
    private readonly IPlayniteAPI _playniteApi;
    private readonly CancellationTokenSource _tokenSource = new();

    public InstallController(DLsiteLibrarySettings settings,
        Game game, IPlayniteAPI playniteApi) : base(game)
    {
        _settings = settings;
        _playniteApi = playniteApi;
        Name = "Download and extract";
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public override async void Install(InstallActionArgs args)
    {
        if (string.IsNullOrEmpty(_settings.ExtractionDir))
        {
            _playniteApi.Dialogs.ShowErrorMessage("Download directory is not set.", "DLsite download");
            return;
        }

        CookieContainer cookieContainer = new CookieContainer();
        cookieContainer.Add(new Cookie("__DLsite_SID", _settings.sId, "/", "dlsite.com"));
        HttpClientHandler handler = new HttpClientHandler { UseCookies = true, CookieContainer = cookieContainer };

        using var client = new HttpClient(handler);
        try
        {
            var apiRes = await client.GetAsync($"https://www.dlsite.com/home/api/=/product.json?workno={Game.GameId}",
                _tokenSource.Token);
            apiRes.EnsureSuccessStatusCode();
            var product = JsonConvert.DeserializeObject<Product[]>(await apiRes.Content.ReadAsStringAsync())[0];

            if (product.is_split_content)
            {
                var dir1 = Path.Combine(_settings.ExtractionDir, product.work_name);
                Directory.CreateDirectory(dir1);

                for (int i = 1; i <= product.content_count; i++)
                {
                    var partRes = await client.GetAsync(
                        $"https://www.dlsite.com/home/download/=/number/{i}/product_id/{Game.GameId}.html",
                        _tokenSource.Token);
                    partRes.EnsureSuccessStatusCode();
                    using Stream content = await partRes.Content.ReadAsStreamAsync();
                    using FileStream file = File.Create(Path.Combine(dir1, product.contents[i].file_name));
                    await content.CopyToAsync(file);
                }

                Game.IsInstalling = false;
                _playniteApi.Dialogs.ShowMessage(
                    string.Format(ResourceProvider.GetString("LOCDLsiteLibrary_Install_SplitArchive"),
                        product.contents[0].file_name), "Split archive");
                return;
            }

            client.DefaultRequestHeaders.Add("Cookie",
                cookieContainer.GetCookieHeader(new Uri("https://play.dlsite.com/")));
            var res = await client.GetAsync($"https://www.dlsite.com/home/download/=/product_id/{Game.GameId}.html",
                _tokenSource.Token);
            res.EnsureSuccessStatusCode();

            var stream = await res.Content.ReadAsStreamAsync();
            using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, false, Encoding.GetEncoding(932));

            archive.ExtractToDirectory(_settings.ExtractionDir);
            var folders = archive.Entries.Where(x => x.FullName.EndsWith("/"))
                .Select(e => e.FullName)
                .ToList();

            var folder = folders.FirstOrDefault();
            if (folders.Count > 1)
            {
                var folderOptions = folders.Select(s => new GenericItemOption { Name = s }).ToList();

                var selectedFolder = _playniteApi.Dialogs.ChooseItemWithSearch(folderOptions, s =>
                        string.IsNullOrWhiteSpace(s)
                            ? folderOptions
                            : folderOptions.Where(o => o.Name.Contains(s)).ToList(),
                    caption: ResourceProvider.GetString("LOCDLsiteLibrary_Install_SelectFolder"));

                if (selectedFolder is not null)
                    folder = selectedFolder.Name;
            }

            string dir = Path.Combine(_settings.ExtractionDir, folder ?? Game.Name);

            string[] exePaths = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories);
            var exePath = exePaths.FirstOrDefault();

            if (exePaths.Length > 1)
            {
                var exeOptions = exePaths.Select(s => new GenericItemOption
                    { Name = s.Replace(dir, string.Empty).TrimStart('\\') }).ToList();

                var selectedExe = _playniteApi.Dialogs.ChooseItemWithSearch(exeOptions,
                    s => string.IsNullOrWhiteSpace(s)
                        ? exeOptions
                        : exeOptions.Where(o => o.Name.Contains(s)).ToList(),
                    caption: ResourceProvider.GetString("LOCDLsiteLibrary_Install_SelectExe"));

                if (selectedExe is not null)
                    exePath = Path.Combine(dir, selectedExe.Name);
            }

            (Game.GameActions ??= []).Add(new GameAction
            {
                Type = GameActionType.File,
                Path = exePath,
                IsPlayAction = true,
            });

            InvokeOnInstalled(new GameInstalledEventArgs(new GameInstallationData
            {
                InstallDirectory = dir
            }));
        }
        catch (Exception e)
        {
            Game.IsInstalling = false;
            var message = e is HttpRequestException
                ? string.Format(ResourceProvider.GetString("LOCDLsiteLibrary_Install_DownloadFailed"), Game.Name)
                : e.Message;

            _playniteApi.Notifications.Add("dlsite_failed", message, NotificationType.Error);
            logger.Error(e, message);
        }
    }
}