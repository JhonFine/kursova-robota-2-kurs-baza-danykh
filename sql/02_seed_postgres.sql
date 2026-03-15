-- Minimal seed data

INSERT INTO "Employees" ("FullName", "Login", "PasswordHash", "Role", "IsActive", "PasswordChangedAtUtc")
VALUES
    ('System Admin', 'admin', 'PBKDF2$120000$o8ZxJa/CpHPSpDtffhQywA==$T6MtPoOkJKndnpAlv0g5lN/wXNd5L7lnM/t7azCLQRg=', 1, TRUE, NOW()),
    ('Rental Manager', 'manager', 'PBKDF2$120000$WEU7D93Nf+d4Dv9zW/oQaQ==$fN1+M21xb+gf9Nf6gWE0e1i9njjb8p001xpBP5KsU+E=', 2, TRUE, NOW())
ON CONFLICT ("Login") DO NOTHING;

INSERT INTO "Clients" ("FullName", "PassportData", "DriverLicense", "Phone", "Blacklisted")
VALUES
    ('Ivan Petrenko', 'KV123456', 'AB123456', '+380501112233', FALSE),
    ('Olena Shevchenko', 'MK654321', 'CD654321', '+380677778899', FALSE)
ON CONFLICT ("DriverLicense") DO NOTHING;

INSERT INTO "Vehicles" ("Make", "Model", "LicensePlate", "Mileage", "DailyRate", "IsAvailable", "ServiceIntervalKm")
VALUES
    ('Toyota', 'Camry', 'AA1234TX', 56000, 70.00, TRUE, 10000),
    ('Skoda', 'Octavia', 'KA5678BH', 91000, 55.00, TRUE, 12000),
    ('Renault', 'Duster', 'AX4411KK', 43000, 62.00, TRUE, 9000)
ON CONFLICT ("LicensePlate") DO NOTHING;

INSERT INTO "ContractSequences" ("Year", "LastNumber")
VALUES (EXTRACT(YEAR FROM NOW())::int, 0)
ON CONFLICT ("Year") DO NOTHING;
