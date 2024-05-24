namespace DownTube.Core.Dtos;

public record VideoInfoDto
{
    public string Title { get; init; } = null!;
    public string Url { get; init; } = null!;
}