using ClosedXML.Excel;
using RaveStudioAI.Rws.Models;
using System.IO;

namespace RaveStudioAI.Rws.Services
{
    public sealed class RwsExcelExporter
    {
        private static readonly string[] BaseColumns =
        [
            "StudyOID",
            "Subject",
            "SiteOID",
            "FolderOID",
            "FolderRepeatKey",
            "FormOID",
            "FormRepeatKey",
            "RecordPosition"
        ];

        public byte[] ExportByForm(IReadOnlyList<RaveDatasetRow> rows)
        {
            using var workbook = new XLWorkbook();

            foreach (var group in rows.GroupBy(x => x.FormOID ?? "Unknown").OrderBy(x => x.Key))
            {
                var sheet = workbook.Worksheets.Add(SafeSheetName(group.Key));
                var formRows = group.ToList();
                var valueColumns = formRows
                    .SelectMany(x => x.Values.Keys)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var columns = BaseColumns.Concat(valueColumns).ToList();

                for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                {
                    sheet.Cell(1, columnIndex + 1).Value = columns[columnIndex];
                }

                for (var rowIndex = 0; rowIndex < formRows.Count; rowIndex++)
                {
                    var row = formRows[rowIndex];
                    var excelRow = rowIndex + 2;

                    sheet.Cell(excelRow, 1).Value = row.StudyOID ?? string.Empty;
                    sheet.Cell(excelRow, 2).Value = row.SubjectKey ?? string.Empty;
                    sheet.Cell(excelRow, 3).Value = row.SiteOID ?? string.Empty;
                    sheet.Cell(excelRow, 4).Value = row.StudyEventOID ?? string.Empty;
                    sheet.Cell(excelRow, 5).Value = row.StudyEventRepeatKey ?? string.Empty;
                    sheet.Cell(excelRow, 6).Value = row.FormOID ?? string.Empty;
                    sheet.Cell(excelRow, 7).Value = row.FormRepeatKey ?? string.Empty;
                    sheet.Cell(excelRow, 8).Value = row.ItemGroupRepeatKey ?? string.Empty;

                    for (var valueColumnIndex = 0; valueColumnIndex < valueColumns.Count; valueColumnIndex++)
                    {
                        row.Values.TryGetValue(valueColumns[valueColumnIndex], out var value);
                        sheet.Cell(excelRow, BaseColumns.Length + valueColumnIndex + 1).Value = value ?? string.Empty;
                    }
                }

                sheet.Columns().AdjustToContents();
            }

            if (!workbook.Worksheets.Any())
            {
                workbook.Worksheets.Add("NoData");
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static string SafeSheetName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '[', ']', '*', '?', '/', '\\', ':' }).ToHashSet();
            var cleaned = new string(name.Select(x => invalidChars.Contains(x) ? '_' : x).ToArray()).Trim();

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                cleaned = "Sheet";
            }

            return cleaned.Length <= 31 ? cleaned : cleaned[..31];
        }
    }
}

