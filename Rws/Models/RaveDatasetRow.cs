namespace RaveStudioAI.Rws.Models
{
    public sealed record RaveDatasetRow
    {
        public string? StudyOID { get; init; }
        public string? MetaDataVersionOID { get; init; }
        public string? SubjectKey { get; init; }
        public string? SiteOID { get; init; }
        public string? StudyEventOID { get; init; }
        public string? StudyEventRepeatKey { get; init; }
        public string? FormOID { get; init; }
        public string? FormRepeatKey { get; init; }
        public string? ItemGroupOID { get; init; }
        public string? ItemGroupRepeatKey { get; init; }
        public Dictionary<string, string?> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

