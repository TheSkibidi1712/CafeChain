using System.Globalization;
using System.Text;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;

namespace CafeChain.Application.Services.AI;

public sealed class VisualSpecificationBuilder : IVisualSpecificationBuilder
{
    private static readonly (string Name, string Mood, string Background, string Composition, string Lighting)[] StyleProfiles =
    [
        ("Minimal Studio", "clean and focused", "warm off-white seamless studio", "centered hero product with generous negative space", "large softbox with subtle fill"),
        ("Luxury Dark", "premium and dramatic", "charcoal stone cafe surface", "low-angle premium hero composition", "soft rim light and controlled highlights"),
        ("Tropical Fresh", "fresh and energetic", "bright natural cafe setting", "diagonal ingredient-led composition", "sunlit diffused daylight"),
        ("Japanese Cafe", "quiet and refined", "light wood Japanese cafe counter", "balanced asymmetrical composition", "soft window light"),
        ("Vietnamese Modern", "familiar and contemporary", "modern Vietnamese cafe table", "commercial menu composition", "warm natural daylight"),
        ("Pastel Lifestyle", "friendly and youthful", "soft pastel lifestyle backdrop", "playful centered composition", "bright diffused lighting"),
        ("Natural Ingredient", "honest and ingredient-forward", "natural linen and wood surface", "ingredients framing the main subject", "directional natural light"),
        ("Commercial Menu", "clear and appetizing", "neutral menu-board studio", "front-facing catalog composition", "even commercial lighting"),
        ("Premium Advertising", "polished and aspirational", "high-end advertising studio", "dynamic hero composition", "cinematic key and rim lighting"),
        ("Outdoor Summer", "cool and refreshing", "sunny outdoor cafe bokeh", "refreshing lifestyle product composition", "bright summer backlight")
    ];
    private static readonly (string Token, string English, string Color)[] DrinkTokens =
    [
        ("CA PHE", "coffee", "dark brown"),
        ("BAC XIU", "Vietnamese milk coffee", "cream brown"),
        ("TRA SUA", "milk tea", "light brown"),
        ("MATCHA", "matcha", "green"),
        ("DAO", "peach slices", "orange"),
        ("CAM", "orange slices", "orange"),
        ("SA", "lemongrass", "light yellow"),
        ("DAU", "strawberry", "red"),
        ("VAI", "lychee", "white"),
        ("CHANH DAY", "passion fruit", "yellow"),
        ("CHANH", "lime", "green")
    ];

    private static readonly (string Token, string Subject, string Color)[] ToppingTokens =
    [
        ("TRAN CHAU DEN", "cooked black tapioca pearls", "black"),
        ("TRAN CHAU", "cooked tapioca pearls", "brown"),
        ("PUDDING", "custard pudding topping", "yellow"),
        ("THACH DUA", "coconut jelly cubes", "translucent white"),
        ("THACH", "dessert jelly cubes", "translucent"),
        ("KEM CHEESE", "cream cheese foam topping", "cream white"),
        ("NHA DAM", "aloe vera cubes", "translucent green")
    ];

    public VisualSpecificationDTO BuildDrink(string name, string description, string? proposedPrompt = null)
    {
        var key = Normalize(name + " " + description);
        var matches = DrinkTokens.Where(x => ContainsToken(key, x.Token)
            && !(x.Token == "CHANH" && ContainsToken(key, "CHANH DAY"))).ToList();
        var ingredients = matches.Select(x => x.English).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList();
        if (ingredients.Count == 0) ingredients.Add("cafe beverage");

        var beverageKind = key.Contains("CA PHE", StringComparison.Ordinal) ? "coffee drink"
            : key.Contains("TRA SUA", StringComparison.Ordinal) ? "milk tea drink"
            : key.Contains("TRA", StringComparison.Ordinal) ? "iced fruit tea"
            : "cafe beverage";
        var primary = $"a clear glass of {string.Join(" ", ingredients)} {beverageKind}";
        var colors = matches.Select(x => x.Color).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (colors.Count == 0) colors.Add("natural beverage colors");
        return Build(
            primary,
            "beverage",
            ingredients,
            colors,
            ["small wooden tray", "fresh ingredients"],
            ["coffee beans"],
            proposedPrompt);
    }

    public VisualSpecificationDTO BuildTopping(string name, string? proposedPrompt = null)
    {
        var key = Normalize(name);
        var match = ToppingTokens.FirstOrDefault(x => ContainsToken(key, x.Token));
        var subject = string.IsNullOrWhiteSpace(match.Subject) ? "cafe topping ingredient" : match.Subject;
        var color = string.IsNullOrWhiteSpace(match.Color) ? "natural ingredient color" : match.Color;
        return Build(
            $"{subject} in a small clean bowl",
            "food ingredient",
            [subject],
            [color],
            ["small ceramic bowl"],
            ["full beverage"],
            proposedPrompt);
    }

    private static VisualSpecificationDTO Build(
        string primary,
        string subjectType,
        List<string> ingredients,
        List<string> colors,
        List<string> secondary,
        List<string> entityExcluded,
        string? proposedPrompt)
    {
        var required = Tokenize(primary)
            .Where(x => x.Length > 2 && !StopWords.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
        var forbidden = new List<string> { "person", "people", "hand", "logo", "text", "watermark", "beer", "wine", "cocktail" };
        forbidden.AddRange(entityExcluded);

        var specific = $"{primary}, professional product photography";
        var ingredientQuery = $"{string.Join(" ", ingredients.Take(3))}, clean cafe product photography";
        var subjectQuery = $"{primary}, centered, clean neutral background";
        var general = $"{subjectType}, realistic commercial food photography";
        var queries = new[] { specific, subjectQuery, ingredientQuery, general }
            .Select(CleanQuery)
            .Where(x => x.Length is >= 3 and <= 200)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        var profile = StyleProfiles[Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(primary)) % StyleProfiles.Length];
        var container = subjectType == "beverage" ? "clear premium cafe glass" : "small matte ceramic bowl";
        var surface = profile.Background.Contains("wood", StringComparison.OrdinalIgnoreCase)
            ? "light natural wood"
            : "clean textured cafe surface";
        var positive = string.Join(", ", new[]
        {
            primary,
            string.Join(", ", ingredients),
            string.Join(", ", colors),
            container,
            profile.Composition,
            "three-quarter front view, 50mm lens",
            profile.Lighting,
            "realistic professional food photography",
            profile.Background,
            "shallow depth of field"
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (!string.IsNullOrWhiteSpace(proposedPrompt) && LooksEnglish(proposedPrompt))
            positive = $"{positive}, {proposedPrompt.Trim()}";

        return new VisualSpecificationDTO
        {
            PrimarySubject = primary,
            SubjectType = subjectType,
            MainIngredients = ingredients,
            SecondaryObjects = secondary,
            DominantColors = colors,
            Background = profile.Background,
            Composition = profile.Composition,
            CameraAngle = "three-quarter front view",
            Lighting = profile.Lighting,
            ImageStyle = "realistic professional food photography",
            StyleProfile = profile.Name,
            Mood = profile.Mood,
            Container = container,
            Surface = surface,
            Garnishes = ingredients.Take(2).ToList(),
            Props = secondary.Take(2).ToList(),
            Lens = "50mm product photography lens",
            DepthOfField = "shallow depth of field, product fully readable",
            ReferencePurpose = subjectType == "beverage"
                ? "preserve product silhouette, color palette and composition"
                : "preserve ingredient texture and serving presentation",
            Orientation = "square",
            RequiredKeywords = required,
            ForbiddenKeywords = forbidden.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            PexelsQueries = queries,
            ComfyPositivePrompt = positive,
            ComfyNegativePrompt = string.Join(", ", forbidden.Concat(
                ["low quality", "blurry", "distorted", "duplicate product", "illustration", "cartoon"]))
        };
    }

    private static readonly HashSet<string> StopWords =
        ["the", "and", "with", "small", "clean", "clear", "glass", "bowl"];

    private static IEnumerable<string> Tokenize(string value) => value
        .ToLowerInvariant()
        .Split([' ', ',', '-', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string CleanQuery(string value) => string.Join(" ", value
        .Replace(";", " ", StringComparison.Ordinal)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static bool LooksEnglish(string value) => value.All(x => x <= 127 || char.IsWhiteSpace(x));

    private static bool ContainsToken(string normalizedText, string token) =>
        $" {normalizedText} ".Contains($" {token} ", StringComparison.Ordinal);

    private static string Normalize(string value)
    {
        var decomposed = value.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : ' ');
        }
        return string.Join(" ", builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
