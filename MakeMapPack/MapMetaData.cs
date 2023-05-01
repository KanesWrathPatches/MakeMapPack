using System.Xml;

namespace MakeMapPack;

internal sealed class MapMetaData
{
    public string Id { get; }
    public List<MetaDataObject> MetaData { get; } = new();

    public MapMetaData(string id)
    {
        Id = id;
    }

    public void SaveXml(XmlDocument document, XmlElement parent)
    {
        XmlElement mapMetaData = document.CreateElement("MapMetaData", "uri:ea.com:eala:asset");
        XmlAttribute id = document.CreateAttribute("id");
        id.Value = Id;
        mapMetaData.Attributes.Append(id);
        foreach (MetaDataObject metaData in MetaData)
        {
            metaData.SaveXml(document, mapMetaData);
        }
        parent.AppendChild(mapMetaData);
    }
}
