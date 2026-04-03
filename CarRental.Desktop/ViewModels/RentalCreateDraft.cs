using CarRental.Desktop.Models;

namespace CarRental.Desktop.ViewModels;

public sealed class RentalCreateDraft : ViewModelBase
{
    private RentalsPageViewModel.ClientOption? _selectedClient;
    private RentalsPageViewModel.VehicleOption? _selectedVehicle;
    private string _clientSearchText = string.Empty;
    private string _vehicleSearchText = string.Empty;
    private DateTime _startDate = DateTime.Today;
    private string _startTime = "10:00";
    private DateTime _endDate = DateTime.Today.AddDays(1);
    private string _endTime = "10:00";
    private string _pickupLocation;
    private string _returnLocation;
    private bool _createInitialPayment;
    private string _initialPaymentAmountInput = string.Empty;
    private PaymentMethod _paymentMethod = PaymentMethod.Cash;
    private PaymentDirection _paymentDirection = PaymentDirection.Incoming;
    private string _paymentNotes = string.Empty;
    private bool _autoPrintContract;
    private string _formMessage = string.Empty;

    public RentalCreateDraft(string defaultLocation)
    {
        _pickupLocation = defaultLocation;
        _returnLocation = defaultLocation;
    }

    public RentalsPageViewModel.ClientOption? SelectedClient
    {
        get => _selectedClient;
        set => SetProperty(ref _selectedClient, value);
    }

    public RentalsPageViewModel.VehicleOption? SelectedVehicle
    {
        get => _selectedVehicle;
        set => SetProperty(ref _selectedVehicle, value);
    }

    public string ClientSearchText
    {
        get => _clientSearchText;
        set => SetProperty(ref _clientSearchText, value);
    }

    public string VehicleSearchText
    {
        get => _vehicleSearchText;
        set => SetProperty(ref _vehicleSearchText, value);
    }

    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            if (SetProperty(ref _startDate, value) && EndDate.Date < value.Date)
            {
                EndDate = value.Date;
            }
        }
    }

    public string StartTime
    {
        get => _startTime;
        set => SetProperty(ref _startTime, value);
    }

    public DateTime EndDate
    {
        get => _endDate;
        set => SetProperty(ref _endDate, value);
    }

    public string EndTime
    {
        get => _endTime;
        set => SetProperty(ref _endTime, value);
    }

    public string PickupLocation
    {
        get => _pickupLocation;
        set
        {
            if (SetProperty(ref _pickupLocation, value) && string.IsNullOrWhiteSpace(ReturnLocation))
            {
                ReturnLocation = value;
            }
        }
    }

    public string ReturnLocation
    {
        get => _returnLocation;
        set => SetProperty(ref _returnLocation, value);
    }

    public bool CreateInitialPayment
    {
        get => _createInitialPayment;
        set => SetProperty(ref _createInitialPayment, value);
    }

    public string InitialPaymentAmountInput
    {
        get => _initialPaymentAmountInput;
        set => SetProperty(ref _initialPaymentAmountInput, value);
    }

    public PaymentMethod PaymentMethod
    {
        get => _paymentMethod;
        set => SetProperty(ref _paymentMethod, value);
    }

    public PaymentDirection PaymentDirection
    {
        get => _paymentDirection;
        set => SetProperty(ref _paymentDirection, value);
    }

    public string PaymentNotes
    {
        get => _paymentNotes;
        set => SetProperty(ref _paymentNotes, value);
    }

    public bool AutoPrintContract
    {
        get => _autoPrintContract;
        set => SetProperty(ref _autoPrintContract, value);
    }

    public string FormMessage
    {
        get => _formMessage;
        set
        {
            if (SetProperty(ref _formMessage, value))
            {
                OnPropertyChanged(nameof(HasFormMessage));
            }
        }
    }

    public bool HasFormMessage => !string.IsNullOrWhiteSpace(FormMessage);

    public void Reset(string defaultLocation)
    {
        SelectedClient = null;
        SelectedVehicle = null;
        ClientSearchText = string.Empty;
        VehicleSearchText = string.Empty;
        StartDate = DateTime.Today;
        StartTime = "10:00";
        EndDate = DateTime.Today.AddDays(1);
        EndTime = "10:00";
        PickupLocation = defaultLocation;
        ReturnLocation = defaultLocation;
        CreateInitialPayment = false;
        InitialPaymentAmountInput = string.Empty;
        PaymentMethod = PaymentMethod.Cash;
        PaymentDirection = PaymentDirection.Incoming;
        PaymentNotes = string.Empty;
        AutoPrintContract = false;
        FormMessage = string.Empty;
    }
}
