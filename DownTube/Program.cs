using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

var videoUrl = "https://www.youtube.com/playlist?list=PLPF4kKOvJ4B1y0sQOXHqMwVphI7SIpxXr";
var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
var audioOnly = true;
var isPlaylist = true;

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
    }
}

if (videoUrl == null)
{
    Console.WriteLine("""
        
            Uso: dotnet run --url <URL_do_Vídeo> --path <Caminho_do_Arquivo> [--audio-only]
        
            --url, -u       Especifica a URL do vídeo do YouTube que você deseja baixar.
            --path, -p      Especifica o caminho onde o vídeo será salvo no seu sistema.
            --audio-only    (Opcional) Baixa apenas o áudio do vídeo em MP3.
        
            Exemplo de uso:
            dotnet run --url 'URL_do_Vídeo' --path 'Caminho_do_Arquivo'
            dotnet run --url 'URL_do_Vídeo' --path 'Caminho_do_Arquivo' --audio-only
            
        """);

    return;
}

Console.WriteLine("Download iniciado. Aguarde...");
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
    var videos = await youtube.Playlists.GetVideosAsync(videoUrl);
    var dirPath = Path.Combine(filePath, playlist.Title.Replace('|', '-'));
    if (!Directory.Exists(dirPath))
    {
        Directory.CreateDirectory(dirPath);
    }

    foreach (var video in videos)
    {
        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Url);
        IStreamInfo streamInfo;
        string filename;
        if (audioOnly)
        {
            streamInfo = streamManifest.GetAudioOnlyStreams()
                .Where(s => s.Container == Container.Mp3 || s.Container == Container.Mp4)
                .GetWithHighestBitrate();

            filename = $"{video.Title}.{streamInfo.Container.Name}";
            await youtube.Videos.Streams.DownloadAsync(streamInfo, Path.Combine(dirPath, filename));
            var inputFilePath = Path.Combine(dirPath, filename);
            var outputFilePath = Path.Combine(dirPath, $"{video.Title}.mp3");
            await ConvertToMp3(inputFilePath, outputFilePath);
            File.Delete(Path.Combine(dirPath, filename));
            Console.WriteLine($"{video.Title}.mp3 foi salvo com sucesso.");
        }
        else
        {
            streamInfo = streamManifest.GetMuxedStreams()
                .Where(s => s.Container == Container.Mp4)
                .GetWithHighestVideoQuality();

            filename = $"{video.Title}.{streamInfo.Container.Name}";
            await youtube.Videos.Streams.DownloadAsync(streamInfo, Path.Combine(dirPath, filename));
            Console.WriteLine($"{filename} foi salvo com sucesso.");
        }
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

async Task DownloadVideo()
{
    var youtube = new YoutubeClient();
    var streamManifest = await youtube.Videos.Streams.GetManifestAsync(videoUrl);
    var video = await youtube.Videos.GetAsync(videoUrl);
    IStreamInfo streamInfo;
    string filename;
    if (audioOnly)
    {
        streamInfo = streamManifest.GetAudioOnlyStreams()
            .Where(s => s.Container == Container.Mp3 || s.Container == Container.Mp4)
            .GetWithHighestBitrate();

        filename = $"{video.Title}.{streamInfo.Container.Name}";
        await youtube.Videos.Streams.DownloadAsync(streamInfo, Path.Combine(filePath, filename));
        var inputFilePath = Path.Combine(filePath, filename);
        var outputFilePath = Path.Combine(filePath, filename);
        await ConvertToMp3(inputFilePath, outputFilePath);
        Console.WriteLine($"{video.Title}.mp3 foi salvo com sucesso.");
    }
    else
    {
        streamInfo = streamManifest.GetMuxedStreams()
            .Where(s => s.Container == Container.Mp4)
            .GetWithHighestVideoQuality();

        filename = $"{video.Title}.{streamInfo.Container.Name}";
        await youtube.Videos.Streams.DownloadAsync(streamInfo, Path.Combine(filePath, filename));
        Console.WriteLine($"{filename} foi salvo com sucesso.");
    }
}