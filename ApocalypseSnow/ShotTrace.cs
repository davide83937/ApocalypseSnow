using System;
using System.IO;

namespace ApocalypseSnow;

public static class ShotTrace
{
    private static readonly object Gate = new();
    public static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ApocalypseSnow", "shot-traces", $"client-{Environment.ProcessId}.log");

    public static void Log(string message)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, message + Environment.NewLine);
        }
        Console.WriteLine(message);
    }
}
