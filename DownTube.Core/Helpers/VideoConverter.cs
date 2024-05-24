using NAudio.Lame;
using NAudio.Wave;

namespace DownTube.Core.Helpers;

public class VideoConverter
{
    public static async Task ConvertToMp3Async(string inputFile, string outputFile)
    {
        await using var reader = new AudioFileReader(inputFile);
        await using var writer = new LameMP3FileWriter(outputFile, reader.WaveFormat, LAMEPreset.VBR_100);
        await reader.CopyToAsync(writer);
    }
}