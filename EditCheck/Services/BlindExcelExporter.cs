using ClosedXML.Excel;
using RaveStudioAI.EditCheck.Models;
using System.IO;

namespace RaveStudioAI.EditCheck.Services;

public static class BlindExcelExporter
{
    public static void Export(IEnumerable<BlindRow> rows, string path)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("BlindReview");
        var headers = new[]
        {
            "BlindOID", "FolderOID", "FormOID", "FieldOID", "LogicText",
            "SuggestedLogicText", "ValidationMessage", "Error"
        };
        for (var column = 1; column <= headers.Length; column++)
            sheet.Cell(1, column).Value = headers[column - 1];

        var rowNumber = 2;
        foreach (var row in rows)
        {
            sheet.Cell(rowNumber, 1).Value = row.BlindOid;
            sheet.Cell(rowNumber, 2).Value = row.FolderOid;
            sheet.Cell(rowNumber, 3).Value = row.FormOid;
            sheet.Cell(rowNumber, 4).Value = row.FieldOid;
            sheet.Cell(rowNumber, 5).Value = row.LogicText;
            sheet.Cell(rowNumber, 6).Value = row.NormalizedLogicText;
            sheet.Cell(rowNumber, 7).Value = row.ValidationMessage;
            sheet.Cell(rowNumber, 8).Value = row.HasError;
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
        sheet.Columns(5, 7).Width = 55;
        sheet.Columns(5, 7).Style.Alignment.WrapText = true;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        workbook.SaveAs(path);
    }
}
