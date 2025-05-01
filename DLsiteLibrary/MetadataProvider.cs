using System;
using System.Collections.Generic;
using System.Linq;
using DLsiteMetadata;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace DLsiteLibrary
{
    public class MetadataProvider : LibraryMetadataProvider
    {
        private readonly ILogger _logger = LogManager.GetLogger();
        private readonly DLsiteScrapper _dLsiteScrapper;
        private readonly DLsiteLibrarySettings _settings;
        private readonly IPlayniteAPI _playniteApi;

        public MetadataProvider(DLsiteLibrarySettings settings, IPlayniteAPI playniteApi)
        {
            _settings = settings;
            _dLsiteScrapper = new DLsiteScrapper(_logger);
            _playniteApi = playniteApi;
        }

        public override GameMetadata GetMetadata(Game game)
        {
            var gameMetadata = new GameMetadata();
            DLsiteScrapperResult scrapperResult = _dLsiteScrapper.ScrapGamePage($"https://www.dlsite.com/home/work/=/product_id/{game.GameId}.html",
                _settings.GetSupportedLanguage()).Result;

            gameMetadata.Description = scrapperResult.Description;
            var features = new List<MetadataProperty>();

            if (_settings.IncludeFileFormat)
                AddFeatures(scrapperResult.FileFormat);

            if (_settings.IncludeProductFormat)
                AddFeatures(scrapperResult.ProductFormat);

            gameMetadata.Features = features.ToHashSet();

            void AddFeatures(IEnumerable<string> formats)
            {
                features.AddRange(formats.Select(format =>
                {
                    var property = _playniteApi.Database.Features
                        .FirstOrDefault(feature =>
                            feature.Name?.Equals(format, StringComparison.OrdinalIgnoreCase) == true);

                    return property is null
                        ? (MetadataProperty)new MetadataNameProperty(format)
                        : new MetadataIdProperty(property.Id);
                }));
            }

            gameMetadata.Name = scrapperResult.Title;

            if (scrapperResult.Age != null)
            {
                var age = scrapperResult.Age switch
                {
                    DLsiteScrapperResult.AgeRating.AllAges => "All ages",
                    DLsiteScrapperResult.AgeRating.RRated => "R-Rated",
                    DLsiteScrapperResult.AgeRating.Adult => "Adult",
                    _ => null
                };
                var ageRating = _playniteApi.Database.AgeRatings
                    .Where(x => x.Name is not null)
                    .FirstOrDefault(rating => rating.Name.Equals(age, StringComparison.OrdinalIgnoreCase));
                MetadataProperty ageProperty = ageRating is null
                    ? new MetadataNameProperty(age )
                    : new MetadataIdProperty(ageRating.Id);
                gameMetadata.AgeRatings = new[] { ageProperty }.ToHashSet();

            }

            gameMetadata.Genres = scrapperResult.Genres == null ? [] : scrapperResult.Genres
                .Select(genre => (genre, _playniteApi.Database.Genres.Where(x => x.Name is not null)
                    .FirstOrDefault(x => x.Name.Equals(genre, StringComparison.OrdinalIgnoreCase))))
                .Select(MetadataProperty (tuple) =>
                {
                    var (genre, property) = tuple;
                    if (property is not null) return new MetadataIdProperty(property.Id);
                    return new MetadataNameProperty(genre);
                })
                .ToHashSet();
            gameMetadata.Icon = new MetadataFile(scrapperResult.Icon);


            var staff = new List<string>();
            void AddStaff(IEnumerable<string> members)
            {
                if (members != null) staff.AddRange(members);
            }
            AddStaff(scrapperResult.Author);
            if (scrapperResult.Circle != null && (scrapperResult.Author == null || !scrapperResult.Author.Contains(scrapperResult.Circle)))
                staff.Add(scrapperResult.Circle);
            if (_settings.IncludeIllustrators) AddStaff(scrapperResult.Illustrators);
            if (_settings.IncludeScenarioWriters) AddStaff(scrapperResult.ScenarioWriters);
            if (_settings.IncludeMusicCreators) AddStaff(scrapperResult.MusicCreators);
            if (_settings.IncludeVoiceActors) AddStaff(scrapperResult.VoiceActors);
            gameMetadata.Developers = staff.Select(name =>
            {
                var company = _playniteApi.Database.Companies
                    .FirstOrDefault(c => c.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);

                return company is null
                    ? (MetadataProperty)new MetadataNameProperty(name)
                    : new MetadataIdProperty(company.Id);
            }).ToHashSet();


            var links = new List<Link>();

            if (scrapperResult.Links != null) links.AddRange(scrapperResult.Links.Select(link => new Link(link.Key, link.Value)));
            gameMetadata.Links = links;

            if (scrapperResult.Rating != null)
            {
                gameMetadata.CommunityScore =  (int)(scrapperResult.Rating * 20);
            }

            var publisher = _playniteApi.Database.Companies
                .Where(x => x.Name is not null)
                .FirstOrDefault(company => company.Name.Equals("DLsite", StringComparison.OrdinalIgnoreCase));

            MetadataProperty pubProperty = publisher is null
                ? new MetadataNameProperty("DLsite")
                : new MetadataIdProperty(publisher.Id);

            gameMetadata.Publishers = new[] { pubProperty }.ToHashSet();

            if (scrapperResult.ReleaseDate != null)
                gameMetadata.ReleaseDate = new ReleaseDate(scrapperResult.ReleaseDate.Value);

            if (scrapperResult.Series != null)
            {
                var series = _playniteApi.Database.Series
                    .Where(x => x.Name is not null)
                    .FirstOrDefault(series => series.Name.Equals(scrapperResult.Series, StringComparison.OrdinalIgnoreCase));

                MetadataProperty seriesProperty = series is null
                    ? new MetadataNameProperty(scrapperResult.Series)
                    : new MetadataIdProperty(series.Id);
                gameMetadata.Series = new[] { seriesProperty }.ToHashSet();
            }

            gameMetadata.CoverImage = new MetadataFile(scrapperResult.MainImage);
            gameMetadata.BackgroundImage = new MetadataFile(scrapperResult.ProductImages[0]);

            return gameMetadata;
        }
    }
}