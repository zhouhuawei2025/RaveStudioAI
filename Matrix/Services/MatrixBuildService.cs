using RaveStudioAI.Matrix.Models;

namespace RaveStudioAI.Matrix.Services;

public sealed class MatrixBuildService
{
    private const string SubjectColumnName = "Subject";

    public MatrixWorkbook Build(IReadOnlyList<string> formOids, IReadOnlyList<string> visitOids,
        IReadOnlyList<MatrixScenarioRow> scenarioRows)
    {
        var issues = new List<MatrixValidationIssue>();
        var formSet = formOids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validVisitOids = visitOids.Append(SubjectColumnName)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var visitSet = validVisitOids.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in scenarioRows)
        {
            if (string.IsNullOrWhiteSpace(row.MatrixName)) issues.Add(Error(row.SourceRowNumber, "Matrix 为空。"));
            if (string.IsNullOrWhiteSpace(row.MatrixOid)) issues.Add(Error(row.SourceRowNumber, "OID 为空。"));
            if (string.IsNullOrWhiteSpace(row.VisitOid)) issues.Add(Error(row.SourceRowNumber, "Folder/Visit OID 为空。"));
            else if (!visitSet.Contains(row.VisitOid)) issues.Add(Warning(row.SourceRowNumber,
                $"Folder/Visit OID '{row.VisitOid}' 不在访视 OID list 中，导出时不会产生 X。"));
            if (string.IsNullOrWhiteSpace(row.FormOid)) issues.Add(Error(row.SourceRowNumber, "Form OID 为空。"));
            else if (!formSet.Contains(row.FormOid)) issues.Add(Warning(row.SourceRowNumber,
                $"Form OID '{row.FormOid}' 不在表单 OID list 中，导出时不会产生 X。"));
        }

        var matrices = scenarioRows
            .Where(x => !string.IsNullOrWhiteSpace(x.MatrixOid))
            .GroupBy(x => x.MatrixOid, StringComparer.OrdinalIgnoreCase)
            .Select(group => new MatrixDefinition
            {
                Name = group.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.MatrixName))?.MatrixName ?? group.Key,
                Oid = group.Key,
                VisitOids = visitOids.ToList(),
                FormOids = formOids.ToList(),
                MarkedCells = group.Where(x => formSet.Contains(x.FormOid) && visitSet.Contains(x.VisitOid))
                    .Select(x => new MatrixCellKey(FindCanonical(validVisitOids, x.VisitOid),
                                                   FindCanonical(formOids, x.FormOid))).ToHashSet()
            }).ToList();

        return new MatrixWorkbook
        {
            FormOids = formOids.ToList(), VisitOids = visitOids.ToList(), Matrices = matrices, Issues = issues
        };
    }

    private static MatrixValidationIssue Warning(int row, string message) =>
        new() { Level = "Warning", SourceRowNumber = row, Message = message };
    private static MatrixValidationIssue Error(int row, string message) =>
        new() { Level = "Error", SourceRowNumber = row, Message = message };
    private static string FindCanonical(IReadOnlyList<string> values, string value) =>
        values.FirstOrDefault(x => x.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? value;
}
