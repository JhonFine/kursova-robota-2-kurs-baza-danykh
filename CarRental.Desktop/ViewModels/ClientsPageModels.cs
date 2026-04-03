using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;

namespace CarRental.Desktop.ViewModels;

public enum ClientPanelMode
{
    Details = 0,
    Create = 1,
    Edit = 2
}

public sealed class ClientEditorDraft : ObservableObject
{
    private string _fullName = string.Empty;
    private string _phone = string.Empty;
    private string _passportData = string.Empty;
    private DateTime? _passportExpirationDate;
    private string _passportSourceFilePath = string.Empty;
    private string? _passportStoredPath;
    private string _driverLicense = string.Empty;
    private DateTime? _driverLicenseExpirationDate;
    private string _driverLicenseSourceFilePath = string.Empty;
    private string? _driverLicenseStoredPath;

    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string PassportData
    {
        get => _passportData;
        set => SetProperty(ref _passportData, value);
    }

    public DateTime? PassportExpirationDate
    {
        get => _passportExpirationDate;
        set => SetProperty(ref _passportExpirationDate, value?.Date);
    }

    public string PassportSourceFilePath
    {
        get => _passportSourceFilePath;
        set
        {
            if (SetProperty(ref _passportSourceFilePath, value))
            {
                OnPropertyChanged(nameof(HasPendingPassportSourceFile));
                OnPropertyChanged(nameof(HasPassportAttachment));
                OnPropertyChanged(nameof(PassportAttachmentSummary));
            }
        }
    }

    public string? PassportStoredPath
    {
        get => _passportStoredPath;
        set
        {
            if (SetProperty(ref _passportStoredPath, value))
            {
                OnPropertyChanged(nameof(HasSavedPassportAttachment));
                OnPropertyChanged(nameof(HasPassportAttachment));
                OnPropertyChanged(nameof(PassportAttachmentSummary));
            }
        }
    }

    public string DriverLicense
    {
        get => _driverLicense;
        set => SetProperty(ref _driverLicense, value);
    }

    public DateTime? DriverLicenseExpirationDate
    {
        get => _driverLicenseExpirationDate;
        set => SetProperty(ref _driverLicenseExpirationDate, value?.Date);
    }

    public string DriverLicenseSourceFilePath
    {
        get => _driverLicenseSourceFilePath;
        set
        {
            if (SetProperty(ref _driverLicenseSourceFilePath, value))
            {
                OnPropertyChanged(nameof(HasPendingDriverLicenseSourceFile));
                OnPropertyChanged(nameof(HasDriverLicenseAttachment));
                OnPropertyChanged(nameof(DriverLicenseAttachmentSummary));
            }
        }
    }

    public string? DriverLicenseStoredPath
    {
        get => _driverLicenseStoredPath;
        set
        {
            if (SetProperty(ref _driverLicenseStoredPath, value))
            {
                OnPropertyChanged(nameof(HasSavedDriverLicenseAttachment));
                OnPropertyChanged(nameof(HasDriverLicenseAttachment));
                OnPropertyChanged(nameof(DriverLicenseAttachmentSummary));
            }
        }
    }

    public bool HasPendingPassportSourceFile => !string.IsNullOrWhiteSpace(PassportSourceFilePath);

    public bool HasSavedPassportAttachment => !string.IsNullOrWhiteSpace(PassportStoredPath);

    public bool HasPassportAttachment => HasPendingPassportSourceFile || HasSavedPassportAttachment;

    public string PassportAttachmentSummary => BuildAttachmentSummary(
        PassportSourceFilePath,
        PassportStoredPath,
        "паспорт");

    public bool HasPendingDriverLicenseSourceFile => !string.IsNullOrWhiteSpace(DriverLicenseSourceFilePath);

    public bool HasSavedDriverLicenseAttachment => !string.IsNullOrWhiteSpace(DriverLicenseStoredPath);

    public bool HasDriverLicenseAttachment => HasPendingDriverLicenseSourceFile || HasSavedDriverLicenseAttachment;

    public string DriverLicenseAttachmentSummary => BuildAttachmentSummary(
        DriverLicenseSourceFilePath,
        DriverLicenseStoredPath,
        "посвідчення");

    public void Reset(string? prefilledPhone = null)
    {
        FullName = string.Empty;
        Phone = prefilledPhone ?? string.Empty;
        PassportData = string.Empty;
        PassportExpirationDate = null;
        PassportStoredPath = null;
        PassportSourceFilePath = string.Empty;
        DriverLicense = string.Empty;
        DriverLicenseExpirationDate = null;
        DriverLicenseStoredPath = null;
        DriverLicenseSourceFilePath = string.Empty;
    }

    public void LoadFrom(ClientsPageViewModel.ClientRow source)
    {
        FullName = source.FullName;
        Phone = source.Phone;
        PassportData = source.PassportData;
        PassportExpirationDate = source.PassportExpirationDate;
        PassportStoredPath = source.PassportStoredPath;
        PassportSourceFilePath = string.Empty;
        DriverLicense = source.DriverLicense;
        DriverLicenseExpirationDate = source.DriverLicenseExpirationDate;
        DriverLicenseStoredPath = source.DriverLicenseStoredPath;
        DriverLicenseSourceFilePath = string.Empty;
    }

    private static string BuildAttachmentSummary(string? sourceFilePath, string? storedPath, string label)
    {
        if (!string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return $"Новий файл {label}: {Path.GetFileName(sourceFilePath)}";
        }

        return string.IsNullOrWhiteSpace(storedPath)
            ? $"Файл {label} ще не додано."
            : $"Файл {label} уже збережено в профілі.";
    }
}
