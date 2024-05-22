using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

string? videoUrl = null;
var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
var audioOnly = false;
var isPlaylist = false;
var ignoreSizeLimit = false;

#if DEBUG
videoUrl = "https://www.youtube.com/watch?v=QyBG1pplx4A";
audioOnly = true;
#endif

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--url":
        case "-u":
            videoUrl = args[i + 1];
            break;
        case "--path":
        case "-p":
            filePath = Path.GetFullPath(args[i + 1]);
            break;
        case "--audio-only":
            audioOnly = true;
            break;
        case "--playlist":
            isPlaylist = true;
            break;
        case "--ignore-size-limit":
            ignoreSizeLimit = true;
            break;
        case "--help":
        case "-h":
            videoUrl = null;
            break;
    }
}

if (videoUrl == null)
{
    Console.WriteLine("""
        
            Uso: dotnet run --url <URL_do_Vídeo> --path <Caminho_do_Arquivo> [--audio-only]
        
            --help, -h          Exibe a mensagem de ajuda.
            --url, -u           Especifica a URL do vídeo do YouTube que você deseja baixar.
            --path, -p          Especifica o caminho onde o vídeo será salvo no seu sistema.
            --audio-only        (Opcional) Baixa apenas o áudio do vídeo em MP3.
            --playlist          (Opcional) Baixa todos os vídeos de uma playlist.
            --ignore-size-limit (Opcional) Ignora o limite de tamanho de arquivo de 10 MB para músicas.
        
            Exemplo de uso:
            dotnet run --url 'URL_do_Vídeo' --path 'Caminho_do_Arquivo'
            dotnet run --url 'URL_do_Vídeo' --path 'Caminho_do_Arquivo' --audio-only
            
        """);

    return;
}

if (!Directory.Exists(filePath))
{
    Console.WriteLine("O caminho não existe. Forneça um caminho válido.");
    return;
}

Console.WriteLine("Iniciando download. Aguarde...");
try
{
    if (isPlaylist)
    {
        await DownloadPlaylist();
    }
    else
    {
        await DownloadVideo();
    }
}
catch (ExistingFileException e)
{
    Console.WriteLine(e.Message);
    return;
}
catch (Exception e)
{
    Console.WriteLine($"Ocorreu um erro ao baixar o vídeo: {e.Message}.");
    return;
}

return;

async Task DownloadPlaylist()
{
    var youtube = new YoutubeClient();
    var playlist = await youtube.Playlists.GetAsync(videoUrl);
    var dirPath = Path.Combine(filePath, NormalizePath(playlist.Title));
    if (!Directory.Exists(dirPath))
    {
        Directory.CreateDirectory(dirPath);
    }

    var videos = (await youtube.Playlists.GetVideosAsync(videoUrl))
        .Select(v => new VideoInfo { Title = NormalizePath(v.Title), Url = v.Url })
        .ToList();

    foreach (var video in videos.ToList().Where(video => IsExistingFile(video.Title, dirPath)))
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
        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Url);
        if (audioOnly)
        {
            await SaveConvertedAudioFile(streamManifest, video, youtube, dirPath);
        }
        else
        {
            await SaveVideoFile(streamManifest, video, youtube, dirPath);
        }

        Console.WriteLine($"[{i + 1}/{videos.Count}]");
    }
}

async Task DownloadVideo()
{
    var youtube = new YoutubeClient();
    var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoUrl);
    var video = await youtube.Videos.GetAsync(videoUrl);
    var videoInfo = new VideoInfo { Title = NormalizePath(video.Title), Url = video.Url };
    var fileExists = IsExistingFile(videoInfo.Title, filePath);
    if (fileExists)
    {
        throw new ExistingFileException($"O arquivo {videoInfo.Title} já existe. Nada a fazer.");
    }

    if (audioOnly)
    {
        await SaveConvertedAudioFile(streamManifest, videoInfo, youtube, filePath);
    }
    else
    {
        await SaveVideoFile(streamManifest, videoInfo, youtube, filePath);
    }
}

async Task ConvertToMp3(string inputFilePath, string outputFilePath)
{
    const string ffmpegPath
        = @"C:\Users\couto\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-7.0-full_build\bin\ffmpeg.exe";

    var args = $"-i \"{inputFilePath}\" -vn -ar 44100 -ac 2 -b:a 192k \"{outputFilePath}\"";
    var processStartInfo = new ProcessStartInfo
    {
        FileName = ffmpegPath
        , Arguments = args
        , UseShellExecute = false
        , RedirectStandardOutput = true
        , CreateNoWindow = true
    };

    using var process = Process.Start(processStartInfo);
    await process?.WaitForExitAsync()!;
}

async Task SaveConvertedAudioFile(StreamManifest streamManifest
    , VideoInfo video
    , YoutubeClient youtube
    , string dirPath)
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
    await youtube.Videos.Streams.DownloadAsync(streamInfo, Path.Combine(dirPath, filename));
    if (streamInfo.Container == Container.Mp4)
    {
        var inputFilePath = Path.Combine(dirPath, filename);
        var outputFilePath = Path.Combine(dirPath, $"{video.Title}.mp3");
        await ConvertToMp3(inputFilePath, outputFilePath);
        File.Delete(Path.Combine(dirPath, filename));
    }

    Console.WriteLine($"{video.Title}.mp3 foi salvo com sucesso.");
}

async Task SaveVideoFile(StreamManifest streamManifest
    , VideoInfo video
    , YoutubeClient youtube
    , string dirPath)
{
    var streamInfo = streamManifest.GetMuxedStreams()
        .Where(s => s.Container == Container.Mp4)
        .GetWithHighestVideoQuality();

    var filename = $"{video.Title}.{streamInfo.Container.Name}";
    await youtube.Videos.Streams.DownloadAsync(streamInfo, Path.Combine(dirPath, filename));
    Console.WriteLine($"{filename} foi salvo com sucesso.");
}

string NormalizePath(string path)
{
    var invalidChars = Path.GetInvalidFileNameChars();
    foreach (var invalidChar in invalidChars)
    {
        path = path.Replace(invalidChar, '-');
    }

    return path;
}

bool IsExistingFile(string videoTitle, string path)
{
    var files = Directory.GetFiles(path);
    return files.Any(f => Path.GetFileNameWithoutExtension(f).Equals(videoTitle, StringComparison.OrdinalIgnoreCase));
}

internal record VideoInfo
{
    public string Title { get; set; } = null!;
    public string Url { get; set; } = null!;
}

internal class ExistingFileException(string message) : Exception(message);