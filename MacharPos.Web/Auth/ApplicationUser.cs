using Microsoft.AspNetCore.Identity;

namespace MacharPos.Web.Auth
{

    public class ApplicationUser : IdentityUser
    {
        // Cashier’s default station (Masaka/Borno)
        public Guid? HomeStationId { get; set; }
    }
}
