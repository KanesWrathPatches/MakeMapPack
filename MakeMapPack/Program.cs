using System.Diagnostics;
using System.Reflection;
using System.Xml;
using MakeMapPack;

Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

if (args.Length <= 0)
{
    Console.WriteLine("No folder provided for maps.");
    Console.WriteLine("Press ENTER to exit.");
    Console.ReadLine();
    return -1;
}
string path = args[0];
if (!Directory.Exists(path))
{
    Console.WriteLine($"Directory {path} not found.");
    Console.WriteLine("Press ENTER to exit.");
    Console.ReadLine();
    return -2;
}

string? patchManifest = null;
if (args.Length >= 2)
{
    patchManifest = args[1];
}

XmlDocument document = new();

XmlElement root;
document.AppendChild(document.CreateXmlDeclaration("1.0", "utf-8", null));
document.AppendChild(root = document.CreateElement("AssetDeclaration", "uri:ea.com:eala:asset"));

string mapPackName = Path.GetFileName(path);
MapMetaData mapMetaData = new(mapPackName);

Console.WriteLine($"Map Pack: {mapPackName}");
Console.WriteLine("Gathering maps...");

string intDirMaps = Path.Combine(Environment.CurrentDirectory, "int");
string outDirMaps = Path.Combine("out", "data", "maps", "official");
string[] baseMapFiles = new[] { "*.map", "*.tga" };
string[] mapFiles = new[] { "map.manifest", "map.bin", "map.relo", "map.imp", "*.cdata" };

foreach (string map in Directory.EnumerateDirectories(path))
{
    string xmlPath = Path.Combine(map, "map.xml");
    if (!File.Exists(xmlPath))
    {
        continue;
    }

    XmlDocument mapXml = new();
    mapXml.Load(xmlPath);
    XmlNode node = mapXml["AssetDeclaration"]!;
    node = node["GameMap"]!;
    string id = node.Attributes!["id"]!.Value!;
    node = node["MapMetaData"]!;
    mapMetaData.MetaData.Add(new MetaDataObject(id, document, node));

    id = Path.GetFileName(map)!;

    Console.WriteLine($"Building and applying leafmod to {id}");
    string intDirMap = Path.Combine(intDirMaps, id);
    string outDirMap = Path.Combine(outDirMaps, id);

    Process mapPatcher = new()
    {
        StartInfo = new ProcessStartInfo(Path.Combine("MapPatcher", "MapPatcher.exe"), $"\"{map}\" -out \"{intDirMap}\"" + (patchManifest is null ? string.Empty : $" -patch \"{patchManifest}\"")),
        EnableRaisingEvents = true
    };
    mapPatcher.Start();
    mapPatcher.WaitForExit();

    if (mapPatcher.ExitCode != 0)
    {
        Console.WriteLine("Error in map patcher, aborting.");
        Console.WriteLine("Press ENTER to exit.");
        Console.ReadLine();
        return -3;
    }
    if (!Directory.Exists(outDirMap))
    {
        Directory.CreateDirectory(outDirMap);
    }
    foreach (string baseMapFile in baseMapFiles)
    {
        foreach (string file in Directory.GetFiles(map, baseMapFile))
        {
            File.Copy(file, Path.Combine(outDirMap, Path.GetFileName(file)), true);
        }
    }
    foreach (string mapFile in mapFiles)
    {
        string intFile = Path.Combine(intDirMap, mapFile);
        foreach (string file in Directory.GetFiles(intDirMap, mapFile, SearchOption.AllDirectories))
        {
            File.Move(file, Path.Combine(outDirMap, Path.GetFileName(file)), true);
        }
    }
}

mapMetaData.SaveXml(document, root);

string metaDataPath = $"MapMetaData_{mapPackName}.xml";
document.Save(metaDataPath);

Console.WriteLine("Building metadata...");
string outDirMeta = Path.Combine(Environment.CurrentDirectory, "out", "data", "additionalmaps");
if (!Directory.Exists(outDirMeta))
{
    Directory.CreateDirectory(outDirMeta);
}

Process wrathed = new()
{
    StartInfo = new ProcessStartInfo(Path.Combine("WrathEd", "WrathEd.exe"), $"-compile \"{Path.Combine(Environment.CurrentDirectory, metaDataPath)}\" -out \"{Path.Combine(outDirMeta, metaDataPath)}\" -nostringhash"),
    EnableRaisingEvents = true
};
wrathed.Start();
wrathed.WaitForExit();

if (wrathed.ExitCode != 0)
{
    Console.WriteLine("Error in metadata, aborting.");
    Console.WriteLine("Press ENTER to exit.");
    Console.ReadLine();
    return -4;
}

if (File.Exists(metaDataPath))
{
    File.Delete(metaDataPath);
}

// TODO: bundle into big
Process makebig = new()
{
    StartInfo = new ProcessStartInfo(Path.Combine("MakeBig", "MakeBig.exe"), $"-f \"{Path.Combine(Environment.CurrentDirectory, "out")}\" -o:\"{path}.big\""),
    EnableRaisingEvents = true
};
makebig.Start();
makebig.WaitForExit();

if (makebig.ExitCode != 0)
{
    Console.WriteLine("Error while creating big file.");
}

Console.WriteLine("Cleaning up...");
if (Directory.Exists("int"))
{
    Directory.Delete("int", true);
}
if (Directory.Exists("out"))
{
    Directory.Delete("out", true);
}
Console.WriteLine("Done.");
Console.WriteLine("Press ENTER to exit.");
Console.ReadLine();

return 0;