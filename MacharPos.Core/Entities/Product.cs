using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MacharPos.Core.Entities
{
    public class Product
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CategoryId { get; set; }
        public ProductCategory? Category { get; set; }
        public Guid StationId { get; set; }
        public Station? Station { get; set; }

        public string Name { get; set; } = "";
        public string? Sku { get; set; }

        // For your LPG refills (sold by cylinder size), we keep UnitType = Piece
        public string UnitType { get; set; } = "Piece"; // "Piece" only in V1
        public decimal DefaultUnitPrice { get; set; } = 0m;

        // For LPG refill variants only (e.g., 12.5kg), EquivalentKg is used to deduct from batch
        public decimal? EquivalentKg { get; set; } = null;

        public bool IsActive { get; set; } = true;
    }
}
