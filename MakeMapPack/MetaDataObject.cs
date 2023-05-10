using System.Xml;

namespace MakeMapPack;

internal sealed class MetaDataObject
{
    public string Id { get; }
    public string Name { get; }
    public XmlNode Obj { get; }

    public MetaDataObject(string id, string name, XmlDocument document, XmlNode source)
    {
        Id = id;
        Name = name;
        Obj = source;
    }

    public void SaveXml(XmlDocument document, XmlElement parent)
    {
        XmlNode node = document.ImportNode(Obj, true);
        XmlAttribute id = document.CreateAttribute("id");
        id.Value = Id;
        node.Attributes!.Append(id);
        XmlAttribute fileName = node.Attributes["FileName"]!;
        fileName.Value = $"Data\\maps\\official\\{Name}\\{Name}.map";
        XmlAttribute isOfficial = node.Attributes["IsOfficial"]!;
        isOfficial.Value = "true";
        parent.AppendChild(node);
    }
}
