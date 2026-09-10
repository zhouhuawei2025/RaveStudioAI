namespace RaveStudioAI.Rws.Models
{
    public sealed class DatasetDownloadJob
    {
        public required Study Study { get; init; }
        public required List<string> SubjectKeys { get; init; }
        public required List<string> FormNames { get; init; }
        public required string DataType { get; init; }
        public bool AddHistoryOnSuccess { get; set; }
        public HashSet<string> CompletedPairs { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool HasFailed { get; set; }

        public int TotalCount => SubjectKeys.Count * FormNames.Count;

        public int CompletedCount => CompletedPairs.Count;

        public static string MakePairKey(string subjectKey, string formOid)
        {
            return $"{subjectKey}||{formOid}";
        }
    }
}

