using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaveStudioAI.Sds.Models;

public class Form
{
    public string? OID { get; set; }
    public string? Name { get; set; }
    public string LogDirection { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

