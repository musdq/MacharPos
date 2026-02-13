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

        // --- Credit controls (Phase 2) ---
        // Only approved customers can be used for credit sales.
        public bool IsCreditApproved { get; set; } = false;

        // Maximum outstanding credit allowed for this customer.
        public decimal CreditLimit { get; set; } = 0m;

        // Default repayment terms in days (used to auto-calc due dates).
        public int CreditDueDays { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }
}
