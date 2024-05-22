using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

var youtube = new YoutubeClient();
var invalidFileChars = Path.GetInvalidFileNameChars();
var invalidPathChars = Path.GetInvalidPathChars();
var outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
var ignoreSizeLimit = false;
var isPlaylist = false;
var audioOnly = false;
string? videoUrl = null;

#if DEBUG
isPlaylist = true;
audioOnly = true;
videoUrl = "https://youtube.com/playlist?list=PLTRU2u_bXJoqnDRqh6FPdrs2T432lpXyM&si=I-YC3D89fkDDxWIf";
#endif

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--url":
        case "-u":
            videoUrl = args[i + 1];
            break;
        case "--output":
        case "-o":
            outputPath = Path.GetFullPath(args[i + 1]);
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
        
            Uso: DownTube.exe --url <URL_DO_VÍDEO> --output <CAMINHO_DO_ARQUIVO> [--audio-only]
        
            --help, -h          Exibe a mensagem de ajuda.
            --url, -u           Especifica a URL do vídeo do YouTube que você deseja baixar.
            --output, -o        Especifica o caminho onde o vídeo será salvo no seu sistema.
            --audio-only        (Opcional) Baixa apenas o áudio do vídeo em MP3 e converte caso seja necessário.
            --playlist          (Opcional) Baixa todos os vídeos de uma playlist.
            --ignore-size-limit (Opcional) Ignora o limite de tamanho de vídeo de 15 MB.
        
            Exemplo de uso:
            DownTube.exe --url 'URL_DO_VÍDEO' --output 'CAMINHO_DO_ARQUIVO'
            DownTube.exe --url 'URL_DO_VÍDEO' --output 'CAMINHO_DO_ARQUIVO' --audio-only --playlist
            
        """);

    return;
}

if (!Directory.Exists(outputPath))
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
    var playlist = await youtube.Playlists.GetAsync(videoUrl);
    outputPath = Path.Combine(outputPath, NormalizeFilenameOrPath(playlist.Title, isPath: true));
    if (!Directory.Exists(outputPath))
    {
        Directory.CreateDirectory(outputPath);
    }

    var videos = (await youtube.Playlists.GetVideosAsync(videoUrl))
        .Select(v => new VideoInfo { Title = NormalizeFilenameOrPath(v.Title), Url = v.Url })
        .ToList();

    foreach (var video in videos.ToList().Where(video => IsExistingFile(video.Title, outputPath)))
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
            await SaveConvertedAudioFile(streamManifest, video);
        }
        else
        {
            await SaveVideoFile(streamManifest, video);
        }

        Console.WriteLine($"[{i + 1}/{videos.Count}]");
    }
}

async Task DownloadVideo()
{
    var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoUrl);
    var video = await youtube.Videos.GetAsync(videoUrl);
    var videoInfo = new VideoInfo { Title = NormalizeFilenameOrPath(video.Title), Url = video.Url };
    var fileExists = IsExistingFile(videoInfo.Title, outputPath);
    if (fileExists)
    {
        throw new ExistingFileException($"O arquivo {videoInfo.Title} já existe. Nada a fazer.");
    }

    if (audioOnly)
    {
        await SaveConvertedAudioFile(streamManifest, videoInfo);
    }
    else
    {
        await SaveVideoFile(streamManifest, videoInfo);
    }
}

async Task ConvertToMp3(string inputFile, string outputFile)
{
    const string ffmpegPath
        = @"C:\Users\couto\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-7.0-full_build\bin\ffmpeg.exe";

    var args = $"-i \"{inputFile}\" -vn -ar 44100 -ac 2 -b:a 192k \"{outputFile}\"";
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

async Task SaveConvertedAudioFile(StreamManifest streamManifest, VideoInfo video)
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
    await youtube.Videos.Streams.DownloadAsync(streamInfo, Path.Combine(outputPath, filename));
    if (streamInfo.Container == Container.Mp4)
    {
        var inputFilePath = Path.Combine(outputPath, filename);
        var outputFilePath = Path.Combine(outputPath, $"{video.Title}.mp3");
        await ConvertToMp3(inputFilePath, outputFilePath);
        File.Delete(Path.Combine(outputPath, filename));
    }

    Console.WriteLine($"{video.Title}.mp3 foi salvo com sucesso.");
}

async Task SaveVideoFile(StreamManifest streamManifest, VideoInfo video)
{
    var streamInfo = streamManifest.GetMuxedStreams()
        .Where(s => s.Container == Container.Mp4)
        .GetWithHighestVideoQuality();

    var filename = $"{video.Title}.{streamInfo.Container.Name}";
    await youtube.Videos.Streams.DownloadAsync(streamInfo, Path.Combine(outputPath, filename));
    Console.WriteLine($"{filename} foi salvo com sucesso.");
}

string NormalizeFilenameOrPath(string path, bool isPath = false)
{
    var invalidChars = isPath ? invalidPathChars : invalidFileChars;
    foreach (var invalidChar in invalidChars)
    {
        switch (invalidChar)
        {
            case '<' or '\u0017':
                path = path.Replace(invalidChar, '(');
                break;
            case '>' or '\u0019':
                path = path.Replace(invalidChar, ')');
                break;
            case '|' or '\u0005' or '\u0006' or ':' or '\\' or '/' or '\u001D' or '\u0007' or '\u000A':
                path = path.Replace(invalidChar, '-');
                break;
            case '\u0013':
                path = path.Replace(invalidChar, '!');
                break;
            case '\u0014':
                path = path.Replace(invalidChar, '$');
                break;
            case '\u0016':
                path = path.Replace(invalidChar, '&');
                break;
            case '*':
                path = path.Replace(invalidChar, '+');
                break;
            default:
                path = path.Remove(invalidChar);
                break;
        }
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