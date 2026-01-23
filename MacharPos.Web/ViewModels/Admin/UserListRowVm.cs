namespace MacharPos.Web.ViewModels.Admin;

public class UserListRowVm
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public bool IsActive { get; set; }
    public string Role { get; set; } = "";
    public string HomeStation { get; set; } = "";
}