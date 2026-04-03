namespace CarRental.Desktop.ViewModels;

[Flags]
public enum PageRefreshArea
{
    None = 0,
    Fleet = 1 << 0,
    Clients = 1 << 1,
    Rentals = 1 << 2,
    Prokat = 1 << 3,
    UserRentals = 1 << 4,
    Reports = 1 << 5,
    Maintenance = 1 << 6,
    Damages = 1 << 7,
    Admin = 1 << 8
}

