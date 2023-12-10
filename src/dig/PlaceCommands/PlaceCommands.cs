[Command("place", Description = "Manage datalayer.place.")]
internal sealed class PlaceCommands
{
    [Command("login", Description = "Log in to datalayer.place.")]
    public LoginCommand Login { get; init; } = new();

    [Command("logout", Description = "Log out of datalayer.place.")]
    public LogoutCommand Logout { get; init; } = new();

    [Command("show", Description = "Show datalayer.place details.")]
    public ShowCommand Show { get; init; } = new();

    [Command("update", Description = "Update the ip address for your datalayer.place proxy.")]
    public UpdateIPCommand UpdateIP { get; init; } = new();
}
