using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecMFS.Common.Models
{
    // Representa la configuración de un nodo individual
    public class DiskNodeConfig
    {
        public int NodeId { get; set; }
        public string BaseUrl { get; set; } = string.Empty;
    }
}

