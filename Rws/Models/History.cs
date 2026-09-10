using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaveStudioAI.Rws.Models
{
    public class History
    {
        public string HistoryName { get; set; } = string.Empty;
        public string HistoryId { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public string StudyName { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public string DataType { get; set; } = "regular";
        public List<string> SubjectKeys { get; set; } = new();
        public List<string> FormNames { get; set; } = new();

        public string Summary => $"{Project}({Environment}) · {SubjectKeys.Count} 受试者 · {FormNames.Count} 表单";

    }
}

