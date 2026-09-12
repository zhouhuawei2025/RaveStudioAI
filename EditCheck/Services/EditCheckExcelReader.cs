using ClosedXML.Excel;
using RaveStudioAI.Common;
using RaveStudioAI.EditCheck.Models;

namespace RaveStudioAI.EditCheck.Services;

public static class EditCheckExcelReader
{
    public static List<QueryRow> ReadQueries(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.First();
        return ExcelHelper.DataRows(sheet).Select(row => new QueryRow
        {
            QueryOid = ExcelHelper.CellText(row, 1),
            FolderOid = ExcelHelper.CellText(row, 2),
            FormOid = ExcelHelper.CellText(row, 3),
            FieldOid = ExcelHelper.CellText(row, 4),
            LogicText = ExcelHelper.CellText(row, 5),
            MessageText = ExcelHelper.CellText(row, 6)
        }).Where(x => !string.IsNullOrWhiteSpace(x.QueryOid) || !string.IsNullOrWhiteSpace(x.FolderOid) ||
                      !string.IsNullOrWhiteSpace(x.FormOid) || !string.IsNullOrWhiteSpace(x.FieldOid) ||
                      !string.IsNullOrWhiteSpace(x.LogicText) || !string.IsNullOrWhiteSpace(x.MessageText)).ToList();
    }

    public static List<BlindRow> ReadBlinds(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.First();
        return ExcelHelper.DataRows(sheet).Select(row => new BlindRow
        {
            BlindOid = ExcelHelper.CellText(row, 1),
            FolderOid = ExcelHelper.CellText(row, 2),
            FormOid = ExcelHelper.CellText(row, 3),
            FieldOid = ExcelHelper.CellText(row, 4),
            LogicText = ExcelHelper.CellText(row, 5)
        }).Where(x => !string.IsNullOrWhiteSpace(x.BlindOid) || !string.IsNullOrWhiteSpace(x.FolderOid) ||
                      !string.IsNullOrWhiteSpace(x.FormOid) || !string.IsNullOrWhiteSpace(x.FieldOid) ||
                      !string.IsNullOrWhiteSpace(x.LogicText)).ToList();
    }
}
