using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaveStudioAI.Sds.Models
{
    internal class DataDictionaryEntry
    {
        public int EntryOID { get; set; }

        public string ItemDataString { get; set; } = string.Empty;

        public bool IsSpecify { get; set; } = false;
    }
}
