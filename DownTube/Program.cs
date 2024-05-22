using NAudio.Lame;
using NAudio.Wave;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

var youtube = new YoutubeClient();
var invalidChars = new Dictionary<char, char>
{
    ['<'] = ' '
    , ['>'] = ' '
    , [':'] = ' '
    , ['\\'] = ' '
    , ['/'] = ' '
    , ['|'] = ' '
    , ['?'] = ' '
    , ['*'] = ' '
    , ['"'] = ' '
    , ['\''] = ' '
    , ['['] = ' '
    , [']'] = ' '
    , ['@'] = ' '
    , ['#'] = ' '
    , ['+'] = ' '
    , [','] = ' '
    , ['.'] = ' '
    , ['\r'] = ' '
    , ['\n'] = ' '
    , ['\t'] = ' '
    , ['\0'] = ' '
    , ['%'] = ' '
    , ['&'] = 'e'
    , ['{'] = ' '
    , ['}'] = ' '
    , ['$'] = 's'
    , ['!'] = ' '
    , ['`'] = ' '
    , ['='] = ' '
    , ['('] = ' '
    , [')'] = ' '
    , ['-'] = ' '
};

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

var downTubeCmd = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "DownTube.exe" : "DownTube";
if (videoUrl == null)
{
    Console.WriteLine($"""
        
            Uso: {downTubeCmd} --url <URL_DO_VÍDEO> --output <CAMINHO_DO_ARQUIVO> [--audio-only]
        
            --help, -h          Exibe a mensagem de ajuda.
            --url, -u           Especifica a URL do vídeo do YouTube que você deseja baixar.
            --output, -o        Especifica o caminho onde o vídeo será salvo no seu sistema.
            --audio-only        (Opcional) Baixa apenas o áudio do vídeo em MP3 e converte caso seja necessário.
            --playlist          (Opcional) Baixa todos os vídeos de uma playlist.
            --ignore-size-limit (Opcional) Ignora o limite de tamanho de vídeo de 15 MB.
        
            Exemplo de uso:
            {downTubeCmd} --url 'URL_DO_VÍDEO' --output 'CAMINHO_DO_ARQUIVO'
            {downTubeCmd} --url 'URL_DO_VÍDEO' --output 'CAMINHO_DO_ARQUIVO' --audio-only --playlist
            
        """);

    return;
}

if (!Directory.Exists(outputPath))
{
    Console.WriteLine("O caminho não existe. Forneça um caminho válido.");
    return;
}

Console.WriteLine("Iniciando programa. Aguarde...");
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

#if DEBUG
    Console.WriteLine(e.StackTrace);
#endif

    return;
}

return;

async Task DownloadPlaylist()
{
    var playlist = await youtube.Playlists.GetAsync(videoUrl);
    outputPath = Path.Combine(outputPath, NormalizeFilenameOrPath(playlist.Title));
    Console.WriteLine($"Os vídeos serão salvos em {outputPath}.");
    if (!Directory.Exists(outputPath))
    {
        Directory.CreateDirectory(outputPath);
    }

    var videos = (await youtube.Playlists.GetVideosAsync(videoUrl))
        .Select(v => new VideoInfo { Title = NormalizeFilenameOrPath(v.Title), Url = v.Url })
        .ToList();

    foreach (var video in videos.ToList().Where(v => IsExistingFile(v.Title, outputPath)))
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

    Console.WriteLine($"O vídeo será salvo em {outputPath}.");
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
    await using var reader = new AudioFileReader(inputFile);
    await using var writer = new LameMP3FileWriter(outputFile, reader.WaveFormat, LAMEPreset.VBR_100);
    await reader.CopyToAsync(writer);
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
        try
        {
            await ConvertToMp3(inputFilePath, outputFilePath);
        }
        finally
        {
            File.Delete(Path.Combine(outputPath, filename));
        }
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

string NormalizeFilenameOrPath(string str)
{
    str = str.ToLower().Normalize(NormalizationForm.FormD);
    var sb = new StringBuilder();
    foreach (var c in str)
    {
        var currentChar = c;
        var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(currentChar);
        if (unicodeCategory == UnicodeCategory.NonSpacingMark)
        {
            continue;
        }

        if (invalidChars.ContainsKey(currentChar))
        {
            currentChar = invalidChars[c];
        }

        sb.Append(currentChar);
    }

    str = sb.ToString().Normalize(NormalizationForm.FormC).Trim();
    str = WhitespacesRegex().Replace(str, "_");
    return str;
}

bool IsExistingFile(string videoTitle, string path)
{
    var files = Directory.GetFiles(path);
    return files.Any(f => Path.GetFileNameWithoutExtension(f).Equals(videoTitle, StringComparison.OrdinalIgnoreCase));
}

internal record VideoInfo
{
    public string Title { get; init; } = null!;
    public string Url { get; init; } = null!;
}

internal class ExistingFileException(string message) : Exception(message);

internal partial class Program
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacesRegex();
}