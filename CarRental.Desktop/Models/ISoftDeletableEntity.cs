namespace CarRental.Desktop.Models;

public interface ISoftDeletableEntity
{
    bool IsDeleted { get; set; }
}

