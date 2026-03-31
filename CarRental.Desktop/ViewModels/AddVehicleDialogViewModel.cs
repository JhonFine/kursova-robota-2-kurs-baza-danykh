using CarRental.Desktop.Infrastructure;
using CarRental.Desktop.Models;

namespace CarRental.Desktop.ViewModels;

public sealed class AddVehicleDialogViewModel : ViewModelBase
{
    private string _make = string.Empty;
    private string _model = string.Empty;
    private string _engineDisplay = string.Empty;
    private string _fuelType = string.Empty;
    private string _transmissionType = string.Empty;
    private string _doorsCount = string.Empty;
    private string _cargoCapacityDisplay = string.Empty;
    private string _consumptionDisplay = string.Empty;
    private bool _hasAirConditioning = true;
    private string _licensePlate = string.Empty;
    private string _mileage = string.Empty;
    private string _dailyRate = string.Empty;
    private string _serviceIntervalKm = "10000";
    private string _photoPath = string.Empty;
    private string _validationMessage = string.Empty;

    public string Make
    {
        get => _make;
        set => SetProperty(ref _make, value);
    }

    public string Model
    {
        get => _model;
        set => SetProperty(ref _model, value);
    }

    public string EngineDisplay
    {
        get => _engineDisplay;
        set => SetProperty(ref _engineDisplay, value);
    }

    public string FuelType
    {
        get => _fuelType;
        set => SetProperty(ref _fuelType, value);
    }

    public string TransmissionType
    {
        get => _transmissionType;
        set => SetProperty(ref _transmissionType, value);
    }

    public string DoorsCount
    {
        get => _doorsCount;
        set => SetProperty(ref _doorsCount, value);
    }

    public string CargoCapacityDisplay
    {
        get => _cargoCapacityDisplay;
        set => SetProperty(ref _cargoCapacityDisplay, value);
    }

    public string ConsumptionDisplay
    {
        get => _consumptionDisplay;
        set => SetProperty(ref _consumptionDisplay, value);
    }

    public bool HasAirConditioning
    {
        get => _hasAirConditioning;
        set => SetProperty(ref _hasAirConditioning, value);
    }

    public string LicensePlate
    {
        get => _licensePlate;
        set => SetProperty(ref _licensePlate, value);
    }

    public string Mileage
    {
        get => _mileage;
        set => SetProperty(ref _mileage, value);
    }

    public string DailyRate
    {
        get => _dailyRate;
        set => SetProperty(ref _dailyRate, value);
    }

    public string ServiceIntervalKm
    {
        get => _serviceIntervalKm;
        set => SetProperty(ref _serviceIntervalKm, value);
    }

    public string PhotoPath
    {
        get => _photoPath;
        set
        {
            if (SetProperty(ref _photoPath, value))
            {
                OnPropertyChanged(nameof(HasPhoto));
            }
        }
    }

    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath);

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public bool TryBuildDraft(out AddVehicleDraft draft, out string validationError)
    {
        validationError = string.Empty;
        draft = default!;

        if (string.IsNullOrWhiteSpace(Make) ||
            string.IsNullOrWhiteSpace(Model) ||
            string.IsNullOrWhiteSpace(LicensePlate) ||
            string.IsNullOrWhiteSpace(Mileage) ||
            string.IsNullOrWhiteSpace(DailyRate) ||
            string.IsNullOrWhiteSpace(ServiceIntervalKm))
        {
            validationError = "Заповніть усі обов'язкові поля перед додаванням.";
            return false;
        }

        if (!VehicleDomainRules.IsValidLicensePlate(VehicleDomainRules.NormalizeLicensePlate(LicensePlate)))
        {
            validationError = "Некоректний формат номера. Використовуйте шаблон AA1234BB.";
            return false;
        }

        draft = new AddVehicleDraft(
            Make.Trim(),
            Model.Trim(),
            EngineDisplay.Trim(),
            FuelType.Trim(),
            TransmissionType.Trim(),
            DoorsCount.Trim(),
            CargoCapacityDisplay.Trim(),
            ConsumptionDisplay.Trim(),
            HasAirConditioning,
            LicensePlate.Trim(),
            Mileage.Trim(),
            DailyRate.Trim(),
            ServiceIntervalKm.Trim(),
            string.IsNullOrWhiteSpace(PhotoPath) ? null : PhotoPath.Trim());

        return true;
    }
}
