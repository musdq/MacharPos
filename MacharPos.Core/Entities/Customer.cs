using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MacharPos.Core.Entities
{
    public class Customer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid StationId { get; set; }
        public Station? Station { get; set; }

        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
