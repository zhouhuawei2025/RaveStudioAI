using ClosedXML.Excel;
using RaveStudioAI.Common;
using RaveStudioAI.Matrix.Models;
using System.IO;

namespace RaveStudioAI.Matrix.Services;

public sealed class MatrixExcelExporter
{
    private const string SubjectColumnName = "Subject";

    public void Export(MatrixWorkbook data, string outputPath)
    {
        using var workbook = new XLWorkbook();
        var sheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < data.Matrices.Count; index++)
        {
            var matrix = data.Matrices[index];
            var name = UniqueSheetName(ExcelHelper.SafeSheetName($"Matrix{index + 1}#{matrix.Oid.ToUpperInvariant()}", "Matrix"), sheetNames);
            WriteMatrixSheet(workbook.Worksheets.Add(name), matrix);
        }

        if (!workbook.Worksheets.Any()) workbook.Worksheets.Add("NoData");
        workbook.SaveAs(outputPath);
    }

    private static void WriteMatrixSheet(IXLWorksheet sheet, MatrixDefinition matrix)
    {
        sheet.Cell(1, 1).Value = $"Matrix: {matrix.Oid}";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 2).Value = SubjectColumnName;
        var visits = matrix.VisitOids.Where(x => !x.Equals(SubjectColumnName, StringComparison.OrdinalIgnoreCase)).ToList();

        for (var i = 0; i < visits.Count; i++) sheet.Cell(1, i + 3).Value = visits[i];
        for (var formIndex = 0; formIndex < matrix.FormOids.Count; formIndex++)
        {
            var formOid = matrix.FormOids[formIndex];
            var row = formIndex + 2;
            sheet.Cell(row, 1).Value = formOid;
            if (matrix.MarkedCells.Contains(new MatrixCellKey(SubjectColumnName, formOid))) sheet.Cell(row, 2).Value = "X";
            for (var visitIndex = 0; visitIndex < visits.Count; visitIndex++)
                if (matrix.MarkedCells.Contains(new MatrixCellKey(visits[visitIndex], formOid)))
                    sheet.Cell(row, visitIndex + 3).Value = "X";
        }

        var lastRow = Math.Max(matrix.FormOids.Count + 1, 1);
        var lastColumn = Math.Max(visits.Count + 2, 2);
        var used = sheet.Range(1, 1, lastRow, lastColumn);
        used.Style.Font.FontName = "Arial";
        used.Style.Font.FontSize = 10;
        used.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        used.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        used.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Range(1, 1, 1, lastColumn).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.SheetView.FreezeColumns(1);
        sheet.Column(1).Width = 24.42;
        for (var column = 2; column <= lastColumn; column++) sheet.Column(column).Width = 11.92;
    }

    private static string UniqueSheetName(string baseName, HashSet<string> used)
    {
        var candidate = baseName;
        var suffix = 1;
        while (!used.Add(candidate))
        {
            var marker = $"_{suffix++}";
            candidate = baseName[..Math.Min(baseName.Length, 31 - marker.Length)] + marker;
        }
        return candidate;
    }
}
