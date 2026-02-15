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
            DLsiteScrapperResult scrapeResult = _dLsiteScrapper.ScrapGamePage(
                $"https://www.dlsite.com/home/work/=/product_id/{game.GameId}.html",
                _settings.GetSupportedLanguage(),
                _settings.CategoryMappingTarget
            ).Result;

            gameMetadata.Description = scrapeResult.Description;
            var features = new List<MetadataProperty>();

            if (scrapeResult.GameProductFormat != null && !_settings.AssignGameProductFormatToGenre)
                AddFeatures(scrapeResult.GameProductFormat);

            if (_settings.IncludeFileFormat)
                AddFeatures(scrapeResult.FileFormat);

            if (_settings.IncludeProductFormat)
                AddFeatures(scrapeResult.ProductFormat);

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

            gameMetadata.Name = scrapeResult.Title;

            if (scrapeResult.Age != null)
            {
                var age = scrapeResult.Age switch
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
                    ? new MetadataNameProperty(age)
                    : new MetadataIdProperty(ageRating.Id);
                gameMetadata.AgeRatings = new[] { ageProperty }.ToHashSet();
            }

            scrapeResult.Genres = [];

            if (scrapeResult.GameProductFormat != null && _settings.AssignGameProductFormatToGenre)
                scrapeResult.Genres.AddRange(scrapeResult.GameProductFormat);

            if (scrapeResult.SupportedLanguages != null && _settings.SupportedLanguagesMappingTarget == "Genres")
                scrapeResult.Genres.AddRange(scrapeResult.SupportedLanguages);

            gameMetadata.Genres = scrapeResult.Genres
                .Select(genre => (genre, _playniteApi.Database.Genres.Where(x => x.Name is not null)
                    .FirstOrDefault(x => x.Name.Equals(genre, StringComparison.OrdinalIgnoreCase))))
                .Select(MetadataProperty (tuple) =>
                {
                    var (genre, property) = tuple;
                    if (property is not null) return new MetadataIdProperty(property.Id);
                    return new MetadataNameProperty(genre);
                })
                .ToHashSet();

            if (scrapeResult.SupportedLanguages != null && _settings.SupportedLanguagesMappingTarget == "Tags")
            {
                scrapeResult.Tags ??= [];
                scrapeResult.Tags?.AddRange(scrapeResult.SupportedLanguages);
            }

            if (scrapeResult.Tags != null)
            {
                gameMetadata.Tags = scrapeResult.Tags
                    .Select(tag => (tag,
                        _playniteApi.Database.Tags.Where(x => x.Name is not null)
                            .FirstOrDefault(x => x.Name.Equals(tag, StringComparison.OrdinalIgnoreCase))))
                    .Select(tuple =>
                    {
                        var (tag, property) = tuple;
                        if (property is not null) return (MetadataProperty)new MetadataIdProperty(property.Id);
                        return new MetadataNameProperty(tag);
                    })
                    .ToHashSet();
            }

            gameMetadata.Icon = new MetadataFile(scrapeResult.Icon);


            var staff = new List<string>();

            void AddStaff(IEnumerable<string> members)
            {
                if (members != null) staff.AddRange(members);
            }

            AddStaff(scrapeResult.Author);
            if (scrapeResult.Circle != null &&
                (scrapeResult.Author == null || !scrapeResult.Author.Contains(scrapeResult.Circle)))
                staff.Add(scrapeResult.Circle);

            if (_settings.IncludeIllustrators) AddStaff(scrapeResult.Illustrators);
            if (_settings.IncludeScenarioWriters) AddStaff(scrapeResult.ScenarioWriters);
            if (_settings.IncludeMusicCreators) AddStaff(scrapeResult.MusicCreators);
            if (_settings.IncludeVoiceActors) AddStaff(scrapeResult.VoiceActors);
            gameMetadata.Developers = staff.Select(name =>
            {
                var company = _playniteApi.Database.Companies
                    .FirstOrDefault(c => c.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);

                return company is null
                    ? (MetadataProperty)new MetadataNameProperty(name)
                    : new MetadataIdProperty(company.Id);
            }).ToHashSet();


            var links = new List<Link>();

            if (scrapeResult.Links != null)
                links.AddRange(scrapeResult.Links.Select(link => new Link(link.Key, link.Value)));
            gameMetadata.Links = links;

            if (scrapeResult.Rating != null)
            {
                gameMetadata.CommunityScore = (int)(scrapeResult.Rating * 20);
            }

            var publisher = _playniteApi.Database.Companies
                .Where(x => x.Name is not null)
                .FirstOrDefault(company => company.Name.Equals("DLsite", StringComparison.OrdinalIgnoreCase));

            MetadataProperty pubProperty = publisher is null
                ? new MetadataNameProperty("DLsite")
                : new MetadataIdProperty(publisher.Id);

            gameMetadata.Publishers = new[] { pubProperty }.ToHashSet();

            if (scrapeResult.ReleaseDate != null)
                gameMetadata.ReleaseDate = new ReleaseDate(scrapeResult.ReleaseDate.Value);

            if (scrapeResult.Series != null)
            {
                var series = _playniteApi.Database.Series
                    .Where(x => x.Name is not null)
                    .FirstOrDefault(series =>
                        series.Name.Equals(scrapeResult.Series, StringComparison.OrdinalIgnoreCase));

                MetadataProperty seriesProperty = series is null
                    ? new MetadataNameProperty(scrapeResult.Series)
                    : new MetadataIdProperty(series.Id);
                gameMetadata.Series = new[] { seriesProperty }.ToHashSet();
            }

            gameMetadata.CoverImage = new MetadataFile(scrapeResult.MainImage);
            gameMetadata.BackgroundImage = new MetadataFile(scrapeResult.ProductImages[0]);

            return gameMetadata;
        }
    }
}