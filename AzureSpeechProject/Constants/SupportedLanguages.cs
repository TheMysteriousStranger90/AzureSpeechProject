namespace AzureSpeechProject.Constants;

internal static class SupportedLanguages
{
    public static readonly IReadOnlyDictionary<string, string> LanguageNames =
        new Dictionary<string, string>
        {
            { "es", "Spanish" },
            { "fr", "French" },
            { "de", "German" },
            { "it", "Italian" },
            { "pt", "Portuguese" },
            { "ja", "Japanese" },
            { "ko", "Korean" },
            { "zh-Hans", "Chinese (Simplified)" },
            { "ru", "Russian" }
        };

    public static readonly IReadOnlyList<string> TranslationLanguages =
        LanguageNames.Keys.ToList();

    public static readonly IReadOnlyList<string> SpeechRecognitionLanguages =
        new List<string> { "en-US" };
}
