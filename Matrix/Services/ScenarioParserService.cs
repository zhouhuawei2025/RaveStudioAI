using ClosedXML.Excel;
using RaveStudioAI.Matrix.Models;
using System.Text.RegularExpressions;

namespace RaveStudioAI.Matrix.Services;

public sealed class ScenarioParserService
{
    private static readonly Regex FormSeparators = new(@"[,，;；\r\n\t ]+", RegexOptions.Compiled);

    public List<MatrixScenarioRow> Parse(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.FirstOrDefault(x =>
            x.Name.Equals("Matrices", StringComparison.OrdinalIgnoreCase)) ?? workbook.Worksheets.First();
        var range = sheet.RangeUsed();
        if (range is null) return [];

        var rows = new List<MatrixScenarioRow>();
        var headerRow = range.FirstRowUsed()?.RowNumber() ?? 1;
        var lastRow = range.LastRowUsed()?.RowNumber() ?? headerRow;

        for (var rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            var matrixName = GetMergedAwareText(sheet, rowNumber, 1);
            var matrixOid = GetMergedAwareText(sheet, rowNumber, 2);
            var visitOid = sheet.Cell(rowNumber, 3).GetString().Trim();
            var formText = sheet.Cell(rowNumber, 4).GetString();

            if (string.IsNullOrWhiteSpace(matrixName) && string.IsNullOrWhiteSpace(matrixOid) &&
                string.IsNullOrWhiteSpace(visitOid) && string.IsNullOrWhiteSpace(formText)) continue;

            var formOids = SplitFormOids(formText).DefaultIfEmpty(string.Empty);
            foreach (var formOid in formOids)
            {
                rows.Add(new MatrixScenarioRow
                {
                    SourceRowNumber = rowNumber,
                    MatrixName = matrixName,
                    MatrixOid = matrixOid,
                    VisitOid = visitOid,
                    FormOid = formOid
                });
            }
        }
        return rows;
    }

    private static string GetMergedAwareText(IXLWorksheet sheet, int row, int column)
    {
        var cell = sheet.Cell(row, column);
        return (cell.IsMerged() ? cell.MergedRange().FirstCell() : cell).GetString().Trim();
    }

    private static IEnumerable<string> SplitFormOids(string text) => FormSeparators
        .Split(text ?? string.Empty)
        .Select(x => x.Trim())
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase);
}
