using DownTube.Core.Dtos;
using DownTube.Core.Exceptions;
using DownTube.Core.Helpers;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace DownTube.Core;

public class Youtube(string videoUrl, string outputPath, bool audioOnly = false, bool ignoreSizeLimit = false)
{
    private readonly YoutubeClient _youtube = new();

    public async Task DownloadPlaylistAsync()
    {
        var playlist = await _youtube.Playlists.GetAsync(videoUrl);
        outputPath = Path.Combine(outputPath, FileHelpers.NormalizeFilenameOrPath(playlist.Title));
        Console.WriteLine($"Os vídeos serão salvos em {outputPath}.");
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        var videos = (await _youtube.Playlists.GetVideosAsync(videoUrl))
            .Select(v => new VideoInfoDto { Title = FileHelpers.NormalizeFilenameOrPath(v.Title), Url = v.Url })
            .ToList();

        foreach (var video in videos.ToList().Where(v => FileHelpers.IsExistingFile(v.Title, outputPath)))
        {
            Console.WriteLine($"O arquivo {video.Title} já existe. Ignorando...");
            videos.Remove(video);
        }

        if (videos.Count == 0)
        {
            throw new ExistingFileException("Todos os vídeos já existem. Nada a fazer.");
        }

        for (var i = 0; i < videos.Count; i++)
        {
            var video = videos[i];
            var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(video.Url);
            if (audioOnly)
            {
                await SaveConvertedAudioFileAsync(streamManifest, video);
            }
            else
            {
                await SaveVideoFileAsync(streamManifest, video);
            }

            Console.WriteLine($"[{i + 1}/{videos.Count}]");
        }
    }

    public async Task DownloadVideoAsync()
    {
        var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(videoUrl);
        var video = await _youtube.Videos.GetAsync(videoUrl);
        var videoInfo = new VideoInfoDto { Title = FileHelpers.NormalizeFilenameOrPath(video.Title), Url = video.Url };
        var fileExists = FileHelpers.IsExistingFile(videoInfo.Title, outputPath);
        if (fileExists)
        {
            throw new ExistingFileException($"O arquivo {videoInfo.Title} já existe. Nada a fazer.");
        }

        Console.WriteLine($"O vídeo será salvo em {outputPath}.");
        if (audioOnly)
        {
            await SaveConvertedAudioFileAsync(streamManifest, videoInfo);
        }
        else
        {
            await SaveVideoFileAsync(streamManifest, videoInfo);
        }
    }

    private async Task SaveConvertedAudioFileAsync(StreamManifest streamManifest, VideoInfoDto video)
    {
        var streamInfo = streamManifest.GetAudioOnlyStreams()
            .Where(s => s.Container == Container.Mp3 || s.Container == Container.Mp4)
            .GetWithHighestBitrate();

        var streamSize = streamInfo.Size.MegaBytes;
        if (streamSize > 15 && !ignoreSizeLimit)
        {
            Console.WriteLine($"O vídeo {video.Title} é muito grande para ser baixado. Ignorando...");
            return;
        }

        var filename = $"{video.Title}.{streamInfo.Container.Name}";
        await _youtube.Videos.Streams.DownloadAsync(streamInfo, Path.Combine(outputPath, filename));
        if (streamInfo.Container == Container.Mp4)
        {
            var inputFilePath = Path.Combine(outputPath, filename);
            var outputFilePath = Path.Combine(outputPath, $"{video.Title}.mp3");
            try
            {
                await VideoConverter.ConvertToMp3Async(inputFilePath, outputFilePath);
            }
            finally
            {
                File.Delete(Path.Combine(outputPath, filename));
            }
        }

        Console.WriteLine($"{video.Title}.mp3 foi salvo com sucesso.");
    }

    private async Task SaveVideoFileAsync(StreamManifest streamManifest, VideoInfoDto video)
    {
        var streamInfo = streamManifest.GetMuxedStreams()
            .Where(s => s.Container == Container.Mp4)
            .GetWithHighestVideoQuality();

        var filename = $"{video.Title}.{streamInfo.Container.Name}";
        await _youtube.Videos.Streams.DownloadAsync(streamInfo, Path.Combine(outputPath, filename));
        Console.WriteLine($"{filename} foi salvo com sucesso.");
    }
}