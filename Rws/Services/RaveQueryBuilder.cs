using RaveStudioAI.Rws.Models;
using System.Collections.Generic;
using System.Xml.Linq;

namespace RaveStudioAI.Rws.Services
{
    public static class RaveQueryBuilder
    {
        public static string BuildQuery(string studyoid, string recipient, QueryRow row)
        {
            var formRepeatNumber = string.IsNullOrWhiteSpace(row.FormRepeatNumber) ? "0" : row.FormRepeatNumber;
            var studyEventRepeatKey = IncrementStringNumber(row.FolderRepeatNumber);
            var formRepeatKey = IncrementStringNumber(formRepeatNumber);
            var ns = (XNamespace)"http://www.cdisc.org/ns/odm/v1.3";
            var mdsolNs = (XNamespace)"http://www.mdsol.com/ns/odm/metadata";

            var queryElement = new XElement(mdsolNs + "Query",
                new XAttribute("Value", row.Query),
                new XAttribute("Recipient", recipient),
                new XAttribute("Status", "Open"),
                new XAttribute("RequiresResponse", "Yes"));

            var itemDataAttributes = new List<XAttribute>
            {
                new XAttribute("ItemOID", row.FieldOID),
                new XAttribute("TransactionType", "Context"),
                new XAttribute("Value", row.Data)
            };

            if (!string.IsNullOrWhiteSpace(row.Specify))
            {
                itemDataAttributes.Add(new XAttribute(mdsolNs + "SpecifyValue", row.Specify));
            }

            var itemDataElement = new XElement(ns + "ItemData",
                itemDataAttributes,
                queryElement);

            var itemGroupDataElement = new XElement(ns + "ItemGroupData",
                new XAttribute("ItemGroupOID", row.FormOID),
                new XAttribute("TransactionType", "Context"),
                new XAttribute(mdsolNs + "Submission", "SpecifiedItemsOnly"),
                itemDataElement);

            if (int.TryParse(row.RecordPosition, out var recordPosition))
            {
                itemGroupDataElement.Add(new XAttribute("ItemGroupRepeatKey", recordPosition.ToString()));
            }

            var formDataElement = new XElement(ns + "FormData",
                new XAttribute("FormOID", row.FormOID),
                new XAttribute("TransactionType", "Context"),
                new XAttribute("FormRepeatKey", formRepeatKey),
                itemGroupDataElement);

            var studyEventDataElement = new XElement(ns + "StudyEventData",
                new XAttribute("StudyEventOID", row.FolderOID),
                new XAttribute("TransactionType", "Update"),
                new XAttribute("StudyEventRepeatKey", studyEventRepeatKey),
                formDataElement);

            var subjectDataElement = new XElement(ns + "SubjectData",
                new XAttribute("SubjectKey", row.Subject),
                new XAttribute(mdsolNs + "SubjectKeyType", "SubjectName"),
                new XAttribute("TransactionType", "Context"),
                new XElement(ns + "SiteRef", new XAttribute("LocationOID", row.SiteOID)),
                studyEventDataElement);

            var clinicalDataElement = new XElement(ns + "ClinicalData",
                new XAttribute("StudyOID", $"{studyoid}"),
                new XAttribute("MetaDataVersionOID", "1"),
                subjectDataElement);

            var odmElement = new XElement(ns + "ODM",
                new XAttribute("ODMVersion", "1.3"),
                new XAttribute("FileType", "Transactional"),
                new XAttribute("CreationDateTime", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")),
                new XAttribute("Originator", "RWS Query Console"),
                new XAttribute("FileOID", Guid.NewGuid().ToString()),
                new XAttribute("Granularity", "AllClinicalData"),
                new XAttribute(XNamespace.Xmlns + "mdsol", "http://www.mdsol.com/ns/odm/metadata"),
                clinicalDataElement);

            return "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n" + odmElement;
        }

        private static string IncrementStringNumber(string value)
        {
            return int.TryParse(value, out var number) ? (number + 1).ToString() : value;
        }
    }
}

