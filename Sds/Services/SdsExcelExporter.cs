using ClosedXML.Excel;
using RaveStudioAI.Sds.Models;

namespace RaveStudioAI.Sds.Services;

internal static class SdsExcelExporter
{
    public static void ExportForms(List<Form> forms, string path)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Forms");
        WriteHeaders(sheet,
        [
            "OID", "Ordinal", "DraftFormName", "DraftFormActive", "HelpText", "IsTemplate",
            "IsSignatureRequired", "IsEproForm", "ViewRestrictions", "EntryRestrictions",
            "LogDirection", "DDEOption", "ConfirmationStyle", "LinkFolderOID", "LinkFormOID"
        ]);

        for (var index = 0; index < forms.Count; index++)
        {
            var row = index + 2;
            var form = forms[index];
            sheet.Cell(row, 1).Value = form.OID;
            sheet.Cell(row, 2).Value = index + 1;
            sheet.Cell(row, 3).Value = form.Name;
            sheet.Cell(row, 4).Value = "TRUE";
            sheet.Cell(row, 6).Value = "FALSE";
            sheet.Cell(row, 7).Value = "TRUE";
            sheet.Cell(row, 8).Value = "FALSE";
            sheet.Cell(row, 11).Value = form.LogDirection;
            sheet.Cell(row, 12).Value = "MustNotDDE";
            sheet.Cell(row, 13).Value = "NoLink";
        }

        Finish(sheet);
        workbook.SaveAs(path);
    }

    public static void ExportDataDictionaries(List<DataDictionary> dictionaries, string path)
    {
        using var workbook = new XLWorkbook();
        var dictionariesSheet = workbook.Worksheets.Add("DataDictionaries");
        WriteHeaders(dictionariesSheet, ["DataDictionaryName", "OID"]);
        for (var index = 0; index < dictionaries.Count; index++)
        {
            dictionariesSheet.Cell(index + 2, 1).Value = dictionaries[index].Name;
            dictionariesSheet.Cell(index + 2, 2).Value = dictionaries[index].OID;
        }
        Finish(dictionariesSheet);

        var entriesSheet = workbook.Worksheets.Add("DataDictionaryEntries");
        WriteHeaders(entriesSheet, ["DataDictionaryName", "CodedData", "Ordinal", "UserDataString", "Specify"]);
        var row = 2;
        foreach (var dictionary in dictionaries)
        {
            for (var index = 0; index < dictionary.DataDictionaryEntries.Count; index++)
            {
                var entry = dictionary.DataDictionaryEntries[index];
                entriesSheet.Cell(row, 1).Value = dictionary.Name;
                entriesSheet.Cell(row, 2).Value = entry.EntryOID;
                entriesSheet.Cell(row, 3).Value = index + 1;
                entriesSheet.Cell(row, 4).Value = entry.ItemDataString;
                entriesSheet.Cell(row, 5).Value = Bool(entry.IsSpecify);
                row++;
            }
        }
        Finish(entriesSheet);
        workbook.SaveAs(path);
    }

    public static void ExportFields(List<List<Field>> fieldGroups, string path)
    {
        var fields = fieldGroups.SelectMany(x => x).ToList();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Fields");
        var headers = new[]
        {
            "FormOID", "FieldOID", "Ordinal", "DraftFieldNumber", "DraftFieldName",
            "DraftFieldActive", "VariableOID", "DataFormat", "DataDictionaryName", "UnitDictionaryName",
            "CodingDictionary", "ControlType", "AcceptableFileExtensions", "IndentLevel", "PreText",
            "FixedUnit", "HeaderText", "HelpText", "SourceDocument", "IsLog", "DefaultValue",
            "SASLabel", "SASFormat", "EproFormat", "IsRequired", "QueryFutureDate", "IsVisible",
            "IsTranslationRequired", "AnalyteName", "IsClinicalSignificance", "QueryNonConformance",
            "OtherVisits", "CanSetRecordDate", "CanSetDataPageDate", "CanSetInstanceDate",
            "CanSetSubjectDate", "DoesNotBreakSignature", "LowerRange", "UpperRange", "NCLowerRange",
            "NCUpperRange", "ViewRestrictions", "EntryRestrictions", "ReviewGroups", "IsVisualVerify",
            "FDownloadedFromObjectId", "FSourceObjectId", "VDownloadedFromObjectId", "VSourceObjectId",
            "FSourceUrlId", "VSourceUrlId", "AnalyteName_ValCol"
        };
        WriteHeaders(sheet, headers);

        for (var index = 0; index < fields.Count; index++) WriteFieldRow(sheet, index + 2, fields[index]);
        Finish(sheet);

        var wrongFormats = fields.GroupBy(x => x.VariableOID)
            .Where(g => g.Select(x => x.DataFormat).Distinct().Count() > 1).SelectMany(g => g).ToList();
        WriteFieldIssues(workbook.Worksheets.Add("FieldsWithWrongDataFormat"), wrongFormats, "DataFormat");

        var wrongDictionaries = fields.GroupBy(x => x.VariableOID)
            .Where(g => g.Select(x => x.DataDictionaryOID).Distinct().Count() > 1).SelectMany(g => g).ToList();
        WriteFieldIssues(workbook.Worksheets.Add("FieldsWithWrongDicOID"), wrongDictionaries, "DataDictionaryOID");

        workbook.SaveAs(path);
    }

    private static void WriteFieldRow(IXLWorksheet sheet, int row, Field field)
    {
        sheet.Cell(row, 1).Value = field.FormOID;
        sheet.Cell(row, 2).Value = field.FieldOID;
        sheet.Cell(row, 3).Value = field.Oridinal;
        sheet.Cell(row, 5).Value = field.DraftFieldName ?? field.FieldOID;
        sheet.Cell(row, 6).Value = Bool(field.DraftFieldActive);
        sheet.Cell(row, 7).Value = field.VariableOID;
        sheet.Cell(row, 8).Value = field.DataFormat;
        sheet.Cell(row, 9).Value = field.DataDictionaryOID;
        sheet.Cell(row, 11).Value = field.CodingDictionary;
        sheet.Cell(row, 12).Value = field.ControlType;
        sheet.Cell(row, 14).Value = 0;
        sheet.Cell(row, 15).Value = field.FieldName;
        sheet.Cell(row, 19).Value = "TRUE";
        sheet.Cell(row, 20).Value = Bool(field.IsGrid);
        sheet.Cell(row, 21).Value = field.DefaultValue;
        sheet.Cell(row, 22).Value = field.SASLabel;
        sheet.Cell(row, 23).Value = field.SASFormat;
        sheet.Cell(row, 25).Value = Bool(field.IsRequired);
        sheet.Cell(row, 26).Value = Bool(field.QueryFutureDate);
        sheet.Cell(row, 27).Value = Bool(field.IsVisible);
        sheet.Cell(row, 28).Value = "FALSE";
        sheet.Cell(row, 29).Value = field.AnalyteName;
        sheet.Cell(row, 30).Value = Bool(field.IsLab);
        sheet.Cell(row, 31).Value = Bool(field.QueryNonConformance);
        for (var column = 32; column <= 36; column++) sheet.Cell(row, column).Value = "FALSE";
        sheet.Cell(row, 37).Value = Bool(field.DoesNotBreakSignature);
        sheet.Cell(row, 45).Value = "FALSE";
    }

    private static void WriteFieldIssues(IXLWorksheet sheet, List<Field> fields, string finalColumn)
    {
        WriteHeaders(sheet, ["FormOID", "FieldOID", "Ordinal", "VariableOID", finalColumn]);
        for (var index = 0; index < fields.Count; index++)
        {
            var row = index + 2;
            var field = fields[index];
            sheet.Cell(row, 1).Value = field.FormOID;
            sheet.Cell(row, 2).Value = field.FieldOID;
            sheet.Cell(row, 3).Value = field.Oridinal;
            sheet.Cell(row, 4).Value = field.VariableOID;
            sheet.Cell(row, 5).Value = finalColumn == "DataFormat" ? field.DataFormat : field.DataDictionaryOID;
        }
        Finish(sheet);
    }

    private static void WriteHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
            sheet.Cell(1, index + 1).Style.Font.Bold = true;
        }
        sheet.SheetView.FreezeRows(1);
    }

    private static void Finish(IXLWorksheet sheet) => sheet.ColumnsUsed().AdjustToContents();
    private static string Bool(bool value) => value ? "TRUE" : "FALSE";
}
