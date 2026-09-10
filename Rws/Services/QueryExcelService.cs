using ClosedXML.Excel;
using RaveStudioAI.Rws.Models;
using System.IO;

namespace RaveStudioAI.Rws.Services
{
    public sealed class QueryExcelService
    {
        public static readonly string[] RequiredColumns =
        [
            "SiteOID",
            "Subject",
            "FolderOID",
            "FolderRepeatNumber",
            "FormOID",
            "FormRepeatNumber",
            "RecordPosition",
            "FieldOID",
            "Data",
            "Specify",
            "Query",
            "Status"
        ];

        public IReadOnlyList<QueryRow> ReadRows(Stream stream)
        {
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();
            var headers = RequiredColumns
                .Select((_, index) => worksheet.Cell(1, index + 1).GetString().Trim())
                .ToList();

            if (!headers.SequenceEqual(RequiredColumns, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"Excel 模板列名不一致。需要列：{string.Join(", ", RequiredColumns)}");
            }

            var rows = new List<QueryRow>();
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                if (IsEmptyRow(worksheet, rowNumber))
                {
                    continue;
                }

                var specifyCell = Cell(worksheet, rowNumber, 10);

                rows.Add(new QueryRow
                {
                    RowNumber = rowNumber,
                    SiteOID = Cell(worksheet, rowNumber, 1),
                    Subject = Cell(worksheet, rowNumber, 2),
                    FolderOID = Cell(worksheet, rowNumber, 3),
                    FolderRepeatNumber = Cell(worksheet, rowNumber, 4),
                    FormOID = Cell(worksheet, rowNumber, 5),
                    FormRepeatNumber = Cell(worksheet, rowNumber, 6),
                    RecordPosition = Cell(worksheet, rowNumber, 7),
                    FieldOID = Cell(worksheet, rowNumber, 8),
                    Data = Cell(worksheet, rowNumber, 9),
                    Specify = string.IsNullOrWhiteSpace(specifyCell) ? null : specifyCell,
                    Query = Cell(worksheet, rowNumber, 11),
                    Status = Cell(worksheet, rowNumber, 12)
                });
            }

            return rows;
        }

        public byte[] ExportRows(IReadOnlyList<QueryRow> rows, bool includeErrorMessage)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Queries");
            var columns = includeErrorMessage
                ? RequiredColumns.Concat(["ErrorMessage"]).ToList()
                : RequiredColumns.ToList();

            for (var index = 0; index < columns.Count; index++)
            {
                worksheet.Cell(1, index + 1).Value = columns[index];
            }

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var excelRow = index + 2;
                worksheet.Cell(excelRow, 1).Value = row.SiteOID;
                worksheet.Cell(excelRow, 2).Value = row.Subject;
                worksheet.Cell(excelRow, 3).Value = row.FolderOID;
                worksheet.Cell(excelRow, 4).Value = row.FolderRepeatNumber;
                worksheet.Cell(excelRow, 5).Value = row.FormOID;
                worksheet.Cell(excelRow, 6).Value = row.FormRepeatNumber;
                worksheet.Cell(excelRow, 7).Value = row.RecordPosition;
                worksheet.Cell(excelRow, 8).Value = row.FieldOID;
                worksheet.Cell(excelRow, 9).Value = row.Data;
                worksheet.Cell(excelRow, 10).Value = row.Specify ?? string.Empty;
                worksheet.Cell(excelRow, 11).Value = row.Query;
                worksheet.Cell(excelRow, 12).Value = row.Status;

                if (includeErrorMessage)
                {
                    worksheet.Cell(excelRow, 13).Value = row.ErrorMessage ?? string.Empty;
                }
            }

            worksheet.Columns().AdjustToContents();

            using var output = new MemoryStream();
            workbook.SaveAs(output);
            return output.ToArray();
        }

        private static bool IsEmptyRow(IXLWorksheet worksheet, int rowNumber)
        {
            return Enumerable.Range(1, RequiredColumns.Length)
                .All(column => string.IsNullOrWhiteSpace(Cell(worksheet, rowNumber, column)));
        }

        private static string Cell(IXLWorksheet worksheet, int rowNumber, int columnNumber)
        {
            return worksheet.Cell(rowNumber, columnNumber).GetFormattedString().Trim();
        }
    }
}

