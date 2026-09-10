using Medidata.RWS.NET.Standard.Helpers;
using RaveStudioAI.Rws.Models;
using System.Xml.Linq;

namespace RaveStudioAI.Rws.Services
{
    public static class OdmDatasetParser
    {
        public static List<RaveDatasetRow> ParseRows(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return new List<RaveDatasetRow>();
            }

            var cleanXml = RwsHelpers.Xml.Sanitize(xml);
            var xmlDocument = RwsHelpers.Xml.GetXDocumentFromString(cleanXml);
            var rows = new List<RaveDatasetRow>();

            foreach (var clinicalData in xmlDocument.Descendants().Where(x => x.Name.LocalName == "ClinicalData"))
            {
                foreach (var subjectData in clinicalData.Elements().Where(x => x.Name.LocalName == "SubjectData"))
                {
                    var siteRef = subjectData.Elements().FirstOrDefault(x => x.Name.LocalName == "SiteRef");

                    foreach (var studyEventData in subjectData.Elements().Where(x => x.Name.LocalName == "StudyEventData"))
                    {
                        foreach (var formData in studyEventData.Elements().Where(x => x.Name.LocalName == "FormData"))
                        {
                            foreach (var itemGroupData in formData.Elements().Where(x => x.Name.LocalName == "ItemGroupData"))
                            {
                                var row = new RaveDatasetRow
                                {
                                    StudyOID = Attr(clinicalData, "StudyOID"),
                                    MetaDataVersionOID = Attr(clinicalData, "MetaDataVersionOID"),
                                    SubjectKey = Attr(subjectData, "SubjectKey"),
                                    SiteOID = Attr(siteRef, "LocationOID"),
                                    StudyEventOID = Attr(studyEventData, "StudyEventOID"),
                                    StudyEventRepeatKey = Attr(studyEventData, "StudyEventRepeatKey"),
                                    FormOID = Attr(formData, "FormOID"),
                                    FormRepeatKey = Attr(formData, "FormRepeatKey"),
                                    ItemGroupOID = Attr(itemGroupData, "ItemGroupOID"),
                                    ItemGroupRepeatKey = Attr(itemGroupData, "ItemGroupRepeatKey")
                                };

                                foreach (var itemData in itemGroupData.Elements().Where(x => x.Name.LocalName.StartsWith("ItemData", StringComparison.Ordinal)))
                                {
                                    var itemOid = Attr(itemData, "ItemOID");
                                    if (string.IsNullOrWhiteSpace(itemOid))
                                    {
                                        continue;
                                    }

                                    var key = ShortItemKey(itemOid);
                                    var isNull = Attr(itemData, "IsNull");
                                    row.Values[key] = string.Equals(isNull, "Yes", StringComparison.OrdinalIgnoreCase)
                                        ? null
                                        : Attr(itemData, "Value");
                                }

                                rows.Add(row);
                            }
                        }
                    }
                }
            }

            return rows;
        }

        private static string? Attr(XElement? element, string name)
        {
            return element?.Attribute(name)?.Value;
        }

        private static string ShortItemKey(string itemOid)
        {
            var separatorIndex = itemOid.LastIndexOf('.');
            return separatorIndex < 0 || separatorIndex == itemOid.Length - 1
                ? itemOid
                : itemOid[(separatorIndex + 1)..];
        }
    }
}

