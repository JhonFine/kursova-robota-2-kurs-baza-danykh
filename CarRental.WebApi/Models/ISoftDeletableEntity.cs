namespace CarRental.WebApi.Models;

public interface ISoftDeletableEntity
{
    bool IsDeleted { get; set; }
}
