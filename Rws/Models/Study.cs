using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaveStudioAI.Rws.Models
{
    public class Study
    {
        public string Oid { get; set; } = string.Empty;

        public string StudyName { get; set; } = string.Empty;

        public string ProtocolName { get; set; } = string.Empty;

        public string Environment { get; set; } = string.Empty;

        public string DisplayName => string.IsNullOrWhiteSpace(Oid)
            ? StudyName
            : Oid;
    }
}

