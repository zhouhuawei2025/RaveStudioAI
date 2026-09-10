using System.Data;

namespace RaveStudioAI.Rws.Models
{
    public sealed class FormPreviewResult
    {
        public string FormOID { get; set; } = string.Empty;

        public int RowCount { get; set; }

        public DataView Rows { get; set; } = new DataTable().DefaultView;

        public string Header => $"{FormOID} ({RowCount})";
    }
}

