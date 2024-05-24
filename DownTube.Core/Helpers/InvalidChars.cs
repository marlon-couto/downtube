namespace DownTube.Core.Helpers;

public record InvalidChars
{
    public readonly Dictionary<char, char> Dict;

    public InvalidChars()
    {
        var invalidFileNameChars = Path.GetInvalidFileNameChars();
        var additionalInvalidChars = new[]
        {
            '<', '>', ':', '\\', '/', '|', '?', '*', '"', '\'', '[', ']', '@', '#', '+', ',', '.', '\r', '\n', '\t'
            , '\0', '%', '&', '{', '}', '$', '!', '`', '=', '(', ')', '-'
        };

        var invalidChars = invalidFileNameChars.Concat(additionalInvalidChars).ToHashSet();
        Dict = invalidChars.ToDictionary(kvp => kvp
            , kvp =>
            {
                return kvp switch
                {
                    '$' => 's'
                    , '&' => 'e'
                    , _ => ' '
                };
            });
    }

    internal void PrintDict()
    {
        foreach (var c in Dict)
        {
            Console.WriteLine($"Key: {c.Key}, Value: {c.Value}");
        }
    }
}