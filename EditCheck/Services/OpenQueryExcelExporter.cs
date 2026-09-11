using ClosedXML.Excel;
using RaveStudioAI.EditCheck.Models;
using System.IO;

namespace RaveStudioAI.EditCheck.Services;

public static class OpenQueryExcelExporter
{
    public static void Export(IEnumerable<QueryRow> rows, string path)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("OpenQueryValidation");
        var headers = new[] { "ecsOID", "FolderOID", "FormOID", "FieldOID", "LogicText", "querymessage", "NormalizedLogicText", "ValidationMessage" };
        for (var column = 1; column <= headers.Length; column++) sheet.Cell(1, column).Value = headers[column - 1];

        var rowNumber = 2;
        foreach (var row in rows)
        {
            sheet.Cell(rowNumber, 1).Value = row.QueryOid;
            sheet.Cell(rowNumber, 2).Value = row.FolderOid;
            sheet.Cell(rowNumber, 3).Value = row.FormOid;
            sheet.Cell(rowNumber, 4).Value = row.FieldOid;
            sheet.Cell(rowNumber, 5).Value = row.LogicText;
            sheet.Cell(rowNumber, 6).Value = row.MessageText;
            sheet.Cell(rowNumber, 7).Value = row.NormalizedLogicText;
            sheet.Cell(rowNumber, 8).Value = row.ValidationMessage;
            rowNumber++;
        }

        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.SheetView.FreezeRows(1);
        sheet.RangeUsed()?.SetAutoFilter();
        sheet.Columns(1, 4).AdjustToContents();
        sheet.Column(5).Width = 48;
        sheet.Column(6).Width = 55;
        sheet.Column(7).Width = 60;
        sheet.Column(8).Width = 60;
        sheet.Columns(5, 8).Style.Alignment.WrapText = true;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        workbook.SaveAs(path);
    }
}
