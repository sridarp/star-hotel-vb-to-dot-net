using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StarHotel.Domain.Entities;

namespace StarHotel.Api.Services;

/// <summary>
/// PDF document generation service — replaces Crystal Reports CRAXDRT COM dependency (BR-21/BR-22)
/// Uses QuestPDF (open-source .NET native)
/// </summary>
public class DocumentService
{
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(ILogger<DocumentService> logger)
    {
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// BR-21: Temporary Receipt — SubTotal = Payment - Deposit, Total = Payment
    /// </summary>
    public byte[] GenerateTemporaryReceipt(Booking booking, Company company)
    {
        _logger.LogInformation("Generating Temporary Receipt for booking {BookingId}", booking.Id);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(20);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Element(header => ComposeHeader(header, company, "TEMPORARY RECEIPT", booking.FormattedId));

                page.Content().Element(content =>
                {
                    content.PaddingVertical(10).Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(3);
                            });

                            AddReceiptRow(table, "Booking No", booking.FormattedId);
                            AddReceiptRow(table, "Guest Name", booking.GuestName);
                            AddReceiptRow(table, "Check-In", booking.GuestCheckIn.ToString("dd MMM yyyy HH:mm"));
                            AddReceiptRow(table, "Check-Out", booking.GuestCheckOut.ToString("dd MMM yyyy HH:mm"));
                            AddReceiptRow(table, "Room Type", booking.RoomType);
                            AddReceiptRow(table, "Room No", booking.RoomNo);

                            // BR-21: SubTotal = Payment - Deposit
                            AddReceiptRow(table, "Sub Total", $"{company.CurrencySymbol} {booking.TemporaryReceiptSubTotal:F2}");
                            AddReceiptRow(table, "Deposit", $"{company.CurrencySymbol} {booking.Deposit:F2}");
                            AddReceiptRowBold(table, "TOTAL", $"{company.CurrencySymbol} {booking.Payment:F2}");
                        });
                    });
                });

                page.Footer().Element(footer => ComposeFooter(footer, booking.CreatedBy));
            });
        }).GeneratePdf();
    }

    /// <summary>
    /// BR-22: Official Receipt — Total = Payment - Refund
    /// </summary>
    public byte[] GenerateOfficialReceipt(Booking booking, Company company)
    {
        _logger.LogInformation("Generating Official Receipt for booking {BookingId}", booking.Id);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(20);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Element(header => ComposeHeader(header, company, "OFFICIAL RECEIPT", booking.FormattedId));

                page.Content().Element(content =>
                {
                    content.PaddingVertical(10).Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(3);
                            });

                            AddReceiptRow(table, "Booking No", booking.FormattedId);
                            AddReceiptRow(table, "Guest Name", booking.GuestName);
                            AddReceiptRow(table, "Check-In", booking.GuestCheckIn.ToString("dd MMM yyyy HH:mm"));
                            AddReceiptRow(table, "Check-Out", booking.GuestCheckOut.ToString("dd MMM yyyy HH:mm"));
                            AddReceiptRow(table, "Room Type", booking.RoomType);
                            AddReceiptRow(table, "Payment", $"{company.CurrencySymbol} {booking.Payment:F2}");
                            AddReceiptRow(table, "Refund", $"{company.CurrencySymbol} {booking.Refund:F2}");
                            // BR-22: Total = Payment - Refund
                            AddReceiptRowBold(table, "TOTAL", $"{company.CurrencySymbol} {booking.OfficialReceiptTotal:F2}");
                        });
                    });
                });

                page.Footer().Element(footer => ComposeFooter(footer, booking.CreatedBy));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, Company company, string receiptType, string bookingId)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text(company.CompanyName).FontSize(16).Bold();
            col.Item().AlignCenter().Text(company.StreetAddress).FontSize(9);
            col.Item().AlignCenter().Text(company.ContactNo).FontSize(9);
            col.Item().PaddingTop(5).AlignCenter().Text(receiptType).FontSize(14).Bold();
            col.Item().AlignCenter().Text($"No: {bookingId}").FontSize(11);
            col.Item().PaddingTop(5).LineHorizontal(1);
        });
    }

    private static void ComposeFooter(IContainer container, string issuedBy)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"Issued by: {issuedBy}").FontSize(8);
                row.RelativeItem().AlignRight().Text($"Date: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8);
            });
        });
    }

    private static void AddReceiptRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Padding(3).Text(label);
        table.Cell().Padding(3).AlignRight().Text(value);
    }

    private static void AddReceiptRowBold(TableDescriptor table, string label, string value)
    {
        table.Cell().Padding(3).Text(text => text.Span(label).Bold());
        table.Cell().Padding(3).AlignRight().Text(text => text.Span(value).Bold());
    }
}