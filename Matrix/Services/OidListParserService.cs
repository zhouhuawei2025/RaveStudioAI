using ClosedXML.Excel;
using RaveStudioAI.Matrix.Models;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RaveStudioAI.Matrix.Services;

public sealed class OidListParserService
{
    private static readonly Regex Separators = new(@"[,，;；\r\n\t ]+", RegexOptions.Compiled);
    private static readonly HashSet<string> HeaderWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "OID", "Form", "Forms", "FormOID", "Form OID", "Visit", "Visits",
        "VisitOID", "Visit OID", "Folder", "FolderOID", "Folder OID"
    };

    public OidTemplate ParseTemplateWorkbook(string path)
    {
        using var workbook = new XLWorkbook(path);
        var foldersSheet = workbook.Worksheets.FirstOrDefault(x =>
            x.Name.Equals("Folders", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.ElementAtOrDefault(0)
            ?? throw new InvalidOperationException("模板文件中没有可读取的 Folders sheet。");
        var formsSheet = workbook.Worksheets.FirstOrDefault(x =>
            x.Name.Equals("Forms", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.ElementAtOrDefault(1)
            ?? throw new InvalidOperationException("模板文件中没有可读取的 Forms sheet。");

        return new OidTemplate
        {
            VisitOids = Normalize(ParseSheet(foldersSheet)),
            FormOids = Normalize(ParseSheet(formsSheet))
        };
    }

    private static List<string> ParseSheet(IXLWorksheet sheet)
    {
        var range = sheet.RangeUsed();
        if (range is null) return [];

        var column = FindLikelyOidColumn(range);
        return range.RowsUsed()
            .Select(row => row.Cell(column).GetString().Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static int FindLikelyOidColumn(IXLRange range)
    {
        var firstRow = range.FirstRowUsed();
        if (firstRow is null) return 1;

        foreach (var cell in firstRow.CellsUsed())
        {
            if (HeaderWords.Contains(cell.GetString().Trim()))
                return cell.Address.ColumnNumber - range.RangeAddress.FirstAddress.ColumnNumber + 1;
        }
        return 1;
    }

    private static IEnumerable<string> Split(string text) => Separators
        .Split(text ?? string.Empty)
        .Select(x => x.Trim())
        .Where(x => !string.IsNullOrWhiteSpace(x));

    private static List<string> Normalize(IEnumerable<string> values) => values
        .SelectMany(Split)
        .Where(x => !HeaderWords.Contains(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}
