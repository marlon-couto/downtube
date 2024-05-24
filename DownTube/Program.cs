using DownTube.Core;
using DownTube.Core.Exceptions;
using System.Runtime.InteropServices;

namespace DownTube;

internal class Program
{
    private static readonly string HomeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static string? _videoUrl;
    private static string _outputPath = Path.Combine(HomeDir, "Downloads");
    private static bool _audioOnly;
    private static bool _isPlaylist;
    private static bool _ignoreSizeLimit;

    private static async Task Main(string[] args)
    {
        string? outputArg = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--url":
                case "-u":
                    _videoUrl = args[i + 1];
                    break;
                case "--output":
                case "-o":
                    outputArg = args[i + 1];
                    break;
                case "--audio-only":
                    _audioOnly = true;
                    break;
                case "--playlist":
                    _isPlaylist = true;
                    break;
                case "--ignore-size-limit":
                    _ignoreSizeLimit = true;
                    break;
                case "--help":
                case "-h":
                    _videoUrl = null;
                    break;
            }
        }

        if (_videoUrl == null)
        {
            var downTubeCmd = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "DownTube.exe" : "DownTube";
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

        if (outputArg != null)
        {
            if (outputArg.StartsWith("~/") || outputArg.StartsWith("~\\"))
            {
                _outputPath = outputArg.Replace("~", HomeDir).Replace('/', Path.DirectorySeparatorChar);
            }
            else
            {
                _outputPath = Path.GetFullPath(outputArg);
            }
        }

        if (!Directory.Exists(_outputPath))
        {
            Console.WriteLine("O caminho não existe. Forneça um caminho válido.");
            return;
        }

        Console.WriteLine("Iniciando programa. Aguarde...");
        var youtube = new Youtube(_videoUrl, _outputPath, _audioOnly, _ignoreSizeLimit);
        try
        {
            if (_isPlaylist)
            {
                await youtube.DownloadPlaylistAsync();
            }
            else
            {
                await youtube.DownloadVideoAsync();
            }
        }
        catch (ExistingFileException e)
        {
            Console.WriteLine(e.Message);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Ocorreu um erro ao baixar o vídeo: {e.Message}.");
        }
    }
}