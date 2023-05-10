using System.Diagnostics;
using MakeMapPack.SAGE;

namespace MakeMapPack;

internal sealed class MapPatcher
{
    public static bool Run(string mapDir, string outDir)
    {
        if (!Directory.Exists(mapDir))
        {
            Console.WriteLine($"Directory {mapDir} not found.");
            return false;
        }

        string xmlPath = Path.Combine(mapDir, "map.xml");
        if (!File.Exists(xmlPath))
        {
            Console.WriteLine($"Directory {mapDir} does not contain a map.");
            return false;
        }

        Console.WriteLine($"Compiling map {Path.GetFileName(mapDir)}...");
        using (Process wrathed = new()
        {
            StartInfo = new ProcessStartInfo(Path.Combine("WrathEd", "WrathEd.exe"), $"-compile \"{xmlPath}\" -map -terrain \"{Path.Combine("WrathEd", "Terrain")}\" -out \"{outDir}\" -nostringhash"),
            EnableRaisingEvents = true
        })
        {
            wrathed.Start();
            wrathed.WaitForExit();
            if ( wrathed.ExitCode != 0 )
            {
                Console.WriteLine("Error: Map compile failed.");
            }
        }

        Console.WriteLine("Patching...");
        string manifestPath = Path.Combine(outDir, "map.manifest");
        Manifest map = new(manifestPath);
        Manifest patch = new("patch.manifest");
        return map.Merge(patch, outDir);
    }
}
