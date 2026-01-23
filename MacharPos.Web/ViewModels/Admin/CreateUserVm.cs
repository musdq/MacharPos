using System.ComponentModel.DataAnnotations;

namespace MacharPos.Web.ViewModels.Admin;

public class CreateUserVm
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required, MinLength(6)]
    public string Password { get; set; } = "";

    [Required]
    public string Role { get; set; } = "Cashier";

    [Required]
    public Guid HomeStationId { get; set; }

    public List<StationOption> Stations { get; set; } = new();
    public List<string> Roles { get; set; } = new();
}

public record StationOption(Guid Id, string Name);