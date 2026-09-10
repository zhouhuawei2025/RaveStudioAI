using ClosedXML.Excel;
using System.IO;

namespace RaveStudioAI.Common;

/// <summary>
/// 所有模块共用的 Excel 基础操作。具体 Sheet 的业务规则仍由各模块负责。
/// </summary>
public static class ExcelHelper
{
    public static IXLWorksheet RequiredSheet(XLWorkbook workbook, string name) =>
        workbook.Worksheets.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidDataException($"Excel 中没有找到必需的工作表：{name}。");

    public static Dictionary<string, int> HeaderMap(IXLWorksheet sheet, int headerRow = 1)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in sheet.Row(headerRow).CellsUsed())
        {
            var header = NormalizeHeader(cell.GetString());
            if (!string.IsNullOrWhiteSpace(header)) map.TryAdd(header, cell.Address.ColumnNumber);
        }
        return map;
    }

    public static int RequiredColumn(Dictionary<string, int> columns, string sheetName, params string[] names)
    {
        var column = OptionalColumn(columns, names);
        return column > 0
            ? column
            : throw new InvalidDataException($"工作表 {sheetName} 缺少必需列：{string.Join(" / ", names)}。");
    }

    public static int OptionalColumn(Dictionary<string, int> columns, params string[] names)
    {
        foreach (var name in names)
            if (columns.TryGetValue(NormalizeHeader(name), out var column)) return column;
        return 0;
    }

    public static IEnumerable<IXLRow> DataRows(IXLWorksheet sheet, int firstDataRow = 2)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? firstDataRow - 1;
        for (var row = firstDataRow; row <= lastRow; row++) yield return sheet.Row(row);
    }

    public static string CellText(IXLRow row, int column) =>
        column <= 0 ? string.Empty : row.Cell(column).GetString().Trim();

    public static bool IsTrue(string value) => value.Equals("TRUE", StringComparison.OrdinalIgnoreCase);

    public static string SafeSheetName(string? name, string fallback = "Sheet")
    {
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars().Concat(['[', ']', '*', '?', '/', '\\', ':']));
        var cleaned = new string((name ?? string.Empty).Select(x => invalid.Contains(x) ? '_' : x).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = fallback;
        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }

    private static string NormalizeHeader(string value) =>
        value.Trim().Replace("_", string.Empty).Replace(" ", string.Empty);
}
