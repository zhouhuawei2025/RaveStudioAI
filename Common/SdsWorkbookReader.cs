using ClosedXML.Excel;

namespace RaveStudioAI.Common;

public static class SdsWorkbookReader
{
    public static SdsReadResult Read(string path)
    {
        using var workbook = new XLWorkbook(path);
        var formsSheet = ExcelHelper.RequiredSheet(workbook, "Forms");
        var fieldsSheet = ExcelHelper.RequiredSheet(workbook, "Fields");
        var foldersSheet = ExcelHelper.RequiredSheet(workbook, "Folders");

        var forms = ReadForms(formsSheet);
        var activeFormOids = forms.Select(x => x.Oid).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new SdsReadResult
        {
            Forms = forms,
            Fields = ReadFields(fieldsSheet, activeFormOids),
            Folders = ReadFolders(foldersSheet)
        };

        ReadTotalMatrices(workbook, result.Forms);
        return result;
    }

    private static List<ProjectForm> ReadForms(IXLWorksheet sheet)
    {
        var columns = ExcelHelper.HeaderMap(sheet);
        var oidColumn = ExcelHelper.RequiredColumn(columns, sheet.Name, "OID");
        var nameColumn = ExcelHelper.OptionalColumn(columns, "DraftFormName", "Name");
        var activeColumn = ExcelHelper.OptionalColumn(columns, "DraftFormActive", "FormActive");
        if (activeColumn <= 0) activeColumn = 4;
        var result = new List<ProjectForm>();

        foreach (var row in ExcelHelper.DataRows(sheet))
        {
            if (!ExcelHelper.IsTrue(ExcelHelper.CellText(row, activeColumn))) continue;
            var oid = ExcelHelper.CellText(row, oidColumn);
            if (string.IsNullOrWhiteSpace(oid)) continue;
            result.Add(new ProjectForm { Oid = oid, Name = ExcelHelper.CellText(row, nameColumn) });
        }
        return result.GroupBy(x => x.Oid, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
    }

    private static List<ProjectField> ReadFields(IXLWorksheet sheet, IReadOnlySet<string> activeFormOids)
    {
        var columns = ExcelHelper.HeaderMap(sheet);
        var formColumn = ExcelHelper.RequiredColumn(columns, sheet.Name, "FormOID");
        var fieldColumn = ExcelHelper.RequiredColumn(columns, sheet.Name, "FieldOID");
        var ordinalColumn = ExcelHelper.OptionalColumn(columns, "Ordinal");
        var variableColumn = ExcelHelper.OptionalColumn(columns, "VariableOID");
        var logColumn = ExcelHelper.OptionalColumn(columns, "IsLog");
        var activeColumn = ExcelHelper.OptionalColumn(columns, "DraftFieldActive", "FieldActive");
        if (activeColumn <= 0) activeColumn = 6;
        var result = new List<ProjectField>();

        foreach (var row in ExcelHelper.DataRows(sheet))
        {
            if (!ExcelHelper.IsTrue(ExcelHelper.CellText(row, activeColumn))) continue;
            var formOid = ExcelHelper.CellText(row, formColumn);
            var fieldOid = ExcelHelper.CellText(row, fieldColumn);
            var ordinalText = ExcelHelper.CellText(row, ordinalColumn);
            if (string.IsNullOrWhiteSpace(formOid) || string.IsNullOrWhiteSpace(fieldOid)) continue;
            if (!activeFormOids.Contains(formOid)) continue;
            result.Add(new ProjectField
            {
                FormOid = formOid,
                FieldOid = fieldOid,
                Ordinal = int.TryParse(ordinalText, out var ordinal) ? ordinal : null,
                VariableOid = ExcelHelper.CellText(row, variableColumn),
                IsLog = ExcelHelper.IsTrue(ExcelHelper.CellText(row, logColumn))
            });
        }
        return result;
    }

    private static List<ProjectFolder> ReadFolders(IXLWorksheet sheet)
    {
        var columns = ExcelHelper.HeaderMap(sheet);
        var oidColumn = ExcelHelper.RequiredColumn(columns, sheet.Name, "OID");
        var nameColumn = ExcelHelper.OptionalColumn(columns, "DraftFolderName", "FolderName", "Name");
        var parentColumn = ExcelHelper.OptionalColumn(columns, "ParentFolderOID", "ParentFolder");
        var reusableColumn = ExcelHelper.OptionalColumn(columns, "IsReusable");
        var result = new List<ProjectFolder>();

        foreach (var row in ExcelHelper.DataRows(sheet))
        {
            var oid = ExcelHelper.CellText(row, oidColumn);
            if (string.IsNullOrWhiteSpace(oid)) continue;
            result.Add(new ProjectFolder
            {
                Oid = oid,
                Name = ExcelHelper.CellText(row, nameColumn),
                ParentFolderOid = ExcelHelper.CellText(row, parentColumn),
                IsReusable = ExcelHelper.IsTrue(ExcelHelper.CellText(row, reusableColumn))
            });
        }
        return result.GroupBy(x => x.Oid, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
    }

    private static void ReadTotalMatrices(XLWorkbook workbook, List<ProjectForm> forms)
    {
        var formsByOid = forms.ToDictionary(x => x.Oid, StringComparer.OrdinalIgnoreCase);
        var totalSheets = workbook.Worksheets.Where(x =>
            x.Name.Contains("TOTAL", StringComparison.OrdinalIgnoreCase));

        foreach (var sheet in totalSheets)
        {
            var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

            // TOTAL Matrix：A列是 FormOID，B列是 Subject，C列起是 FolderOID。
            for (var rowNumber = 3; rowNumber <= lastRow; rowNumber++)
            {
                var formOid = sheet.Cell(rowNumber, 1).GetString().Trim();
                if (string.IsNullOrWhiteSpace(formOid)) continue;
                if (!formsByOid.TryGetValue(formOid, out var form)) continue;

                for (var columnNumber = 3; columnNumber <= lastColumn; columnNumber++)
                {
                    if (sheet.Cell(rowNumber, columnNumber).IsEmpty()) continue;
                    var folderOid = sheet.Cell(1, columnNumber).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(folderOid)) continue;
                    if (!form.FolderOids.Contains(folderOid, StringComparer.OrdinalIgnoreCase))
                        form.FolderOids.Add(folderOid);
                }
            }
        }
    }

}
