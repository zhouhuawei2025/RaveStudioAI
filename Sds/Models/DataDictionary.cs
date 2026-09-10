using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaveStudioAI.Sds.Models
{
    internal class DataDictionary
    {
        public string OID { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<DataDictionaryEntry> DataDictionaryEntries { get; set; } = new List<DataDictionaryEntry>();
    }
}
