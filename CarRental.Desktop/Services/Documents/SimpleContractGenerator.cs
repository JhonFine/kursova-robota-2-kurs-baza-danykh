using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;
using System.Text;
using OpenXmlDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using QuestDocument = QuestPDF.Fluent.Document;

namespace CarRental.Desktop.Services.Documents;

public sealed class SimpleContractGenerator(string contractsDirectory) : IDocumentGenerator
{
    static SimpleContractGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<GeneratedContractFiles> GenerateRentalContractAsync(
        ContractData data,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(contractsDirectory);

        var stem = $"{data.ContractNumber}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        var textPath = Path.Combine(contractsDirectory, $"{stem}.txt");
        var docxPath = Path.Combine(contractsDirectory, $"{stem}.docx");
        var pdfPath = Path.Combine(contractsDirectory, $"{stem}.pdf");

        var text = BuildTextContent(data);
        await File.WriteAllTextAsync(textPath, text, Encoding.UTF8, cancellationToken);
        GenerateDocx(docxPath, data);
        GeneratePdf(pdfPath, data);

        return new GeneratedContractFiles(textPath, docxPath, pdfPath);
    }

    private static string BuildTextContent(ContractData data)
    {
        return new StringBuilder()
            .AppendLine("ДОГОВІР ОРЕНДИ АВТОМОБІЛЯ")
            .AppendLine($"Номер договору: {data.ContractNumber}")
            .AppendLine($"ID оренди: {data.RentalId}")
            .AppendLine($"Клієнт: {data.ClientName}")
            .AppendLine($"Авто: {data.Vehicle}")
            .AppendLine($"Період оренди: {data.StartDate:dd.MM.yyyy} - {data.EndDate:dd.MM.yyyy}")
            .AppendLine($"Сума до сплати: {data.TotalAmount:C}")
            .AppendLine()
            .AppendLine("Підпис орендодавця: ____________________")
            .AppendLine("Підпис орендаря: ____________________")
            .ToString();
    }

    private static void GenerateDocx(string path, ContractData data)
    {
        using var word = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = word.AddMainDocumentPart();
        main.Document = new OpenXmlDocument(new Body(
            Paragraph("ДОГОВІР ОРЕНДИ АВТОМОБІЛЯ", 28),
            Paragraph($"Номер договору: {data.ContractNumber}", 24),
            Paragraph($"ID оренди: {data.RentalId}", 24),
            Paragraph($"Клієнт: {data.ClientName}", 24),
            Paragraph($"Авто: {data.Vehicle}", 24),
            Paragraph($"Період: {data.StartDate:dd.MM.yyyy} - {data.EndDate:dd.MM.yyyy}", 24),
            Paragraph($"Сума до сплати: {data.TotalAmount:C}", 24),
            Paragraph(string.Empty, 24),
            Paragraph("Підпис орендодавця: ____________________", 24),
            Paragraph("Підпис орендаря: ____________________", 24)));
    }

    private static Paragraph Paragraph(string text, int fontSizeHalfPoints)
    {
        return new Paragraph(
            new Run(
                new RunProperties(new FontSize { Val = fontSizeHalfPoints.ToString() }),
                new Text(text)));
    }

    private static void GeneratePdf(string path, ContractData data)
    {
        QuestDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);
                    page.Content().Column(column =>
                    {
                        column.Spacing(10);
                        column.Item().Text("ДОГОВІР ОРЕНДИ АВТОМОБІЛЯ").FontSize(20).SemiBold();
                        column.Item().Text($"Номер договору: {data.ContractNumber}");
                        column.Item().Text($"ID оренди: {data.RentalId}");
                        column.Item().Text($"Клієнт: {data.ClientName}");
                        column.Item().Text($"Авто: {data.Vehicle}");
                        column.Item().Text($"Період: {data.StartDate:dd.MM.yyyy} - {data.EndDate:dd.MM.yyyy}");
                        column.Item().Text($"Сума до сплати: {data.TotalAmount:C}");
                        column.Item().PaddingTop(30).Text("Підпис орендодавця: ____________________");
                        column.Item().Text("Підпис орендаря: ____________________");
                    });
                });
            })
            .GeneratePdf(path);
    }
}
