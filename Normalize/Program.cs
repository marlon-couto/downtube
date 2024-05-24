using DownTube.Core.Helpers;

namespace Normalize;

internal class Program
{
    private static readonly string HomeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static string _path = Directory.GetCurrentDirectory();

    private static void Main(string[] args)
    {
        string? pathArg = null;
        if (args.Length == 1)
        {
            pathArg = args[0];
        }

        if (pathArg != null)
        {
            if (pathArg.StartsWith("~/") || pathArg.StartsWith("~\\"))
            {
                _path = pathArg.Replace("~", HomeDir).Replace('/', Path.DirectorySeparatorChar);
            }
            else
            {
                _path = Path.GetFullPath(pathArg);
            }
        }

        var files = Directory.GetFiles(_path);
        foreach (var file in files)
        {
            var normalizedFilename = FileHelpers.NormalizeFilenameOrPath(Path.GetFileNameWithoutExtension(file));
            var newFilename = $"{normalizedFilename}{Path.GetExtension(file)}";
            File.Move(file, Path.Combine(_path, newFilename));
        }

        Console.WriteLine("Tarefas concluídas.");
    }
}