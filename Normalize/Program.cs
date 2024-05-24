using DownTube.Core.Helpers;

namespace Normalize;

internal class Program
{
    private static string _path = Directory.GetCurrentDirectory();

    private static void Main(string[] args)
    {
        if (args.Length == 1)
        {
            _path = Path.GetFullPath(args[0]);
        }

        var files = Directory.GetFiles(_path);
        foreach (var file in files)
        {
            FileHelpers.NormalizeFilenameOrPath(file);
        }

        Console.WriteLine("Tarefas concluídas.");
    }
}