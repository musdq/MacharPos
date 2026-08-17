using MacharPos.Web.ViewModels.Expenses;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MacharPos.Web.Services.Expenses;

public interface IExpenseReportPdfService
{
    byte[] Generate(ExpenseReportVm report);
}

public sealed class ExpenseReportPdfService : IExpenseReportPdfService
{
    private const string BrandRed = "#9F1D20";

    public byte[] Generate(ExpenseReportVm report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(style => style.FontSize(8.5f));

                page.Header()
                    .BorderBottom(2)
                    .BorderColor(BrandRed)
                    .PaddingBottom(10)
                    .Row(row =>
                    {
                        row.RelativeItem().Column(column =>
                        {
                            column.Item().Text("MACHAR OIL & GAS LTD")
                                .Bold().FontSize(17).FontColor(BrandRed);
                            column.Item().Text("Expense Report")
                                .SemiBold().FontSize(12);
                        });

                        row.ConstantItem(190).AlignRight().Column(column =>
                        {
                            column.Item().AlignRight().Text(report.PeriodLabel).SemiBold();
                            column.Item().AlignRight().Text(
                                $"{report.RangeStartLocal:dd MMM yyyy} - {report.RangeEndLocal:dd MMM yyyy}");
                        });
                    });

                page.Content().PaddingVertical(14).Column(column =>
                {
                    column.Spacing(12);

                    column.Item().Row(row =>
                    {
                        SummaryCard(row.RelativeItem(), "Paid expenses", report.PaidAmount, report.PaidCount, Colors.Green.Darken2);
                        row.ConstantItem(8);
                        SummaryCard(row.RelativeItem(), "Approved, unpaid", report.ApprovedUnpaidAmount, report.ApprovedUnpaidCount, Colors.Blue.Darken2);
                        row.ConstantItem(8);
                        SummaryCard(row.RelativeItem(), "Pending requests", report.PendingAmount, report.PendingCount, Colors.Orange.Darken2);
                    });

                    column.Item().Text(text =>
                    {
                        text.Span("Filters: ").SemiBold();
                        text.Span(BuildFilterDescription(report));
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(62);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(2.1f);
                            columns.ConstantColumn(48);
                            columns.ConstantColumn(54);
                            columns.ConstantColumn(72);
                        });

                        table.Header(header =>
                        {
                            HeaderCell(header.Cell()).Text("Date / time");
                            HeaderCell(header.Cell()).Text("Station");
                            HeaderCell(header.Cell()).Text("Requested by");
                            HeaderCell(header.Cell()).Text("Category");
                            HeaderCell(header.Cell()).Text("Reason");
                            HeaderCell(header.Cell()).Text("Status");
                            HeaderCell(header.Cell()).Text("Payment");
                            HeaderCell(header.Cell()).AlignRight().Text("Amount");
                        });

                        if (report.Items.Count == 0)
                        {
                            table.Cell().ColumnSpan(8)
                                .BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                                .Padding(12).AlignCenter()
                                .Text("No expense requests matched the selected filters.")
                                .FontColor(Colors.Grey.Darken1);
                        }
                        else
                        {
                            foreach (var item in report.Items)
                            {
                                BodyCell(table.Cell()).Text(item.ExpenseDateTimeLocal.ToString("dd MMM yy\nHH:mm"));
                                BodyCell(table.Cell()).Text(item.StationName);
                                BodyCell(table.Cell()).Text(item.RequesterName);
                                BodyCell(table.Cell()).Text(ExpenseReporting.CategoryName(item.Category));
                                BodyCell(table.Cell()).Text(item.Reason);
                                BodyCell(table.Cell()).Text(ExpenseReporting.StatusName(item.Status));
                                BodyCell(table.Cell()).Text(item.Status != MacharPos.Core.Entities.ExpenseStatus.Approved
                                    ? "—"
                                    : item.PaymentStatus.ToString());
                                BodyCell(table.Cell()).AlignRight().Text($"₦{item.Amount:N2}");
                            }
                        }
                    });
                });

                page.Footer()
                    .BorderTop(1)
                    .BorderColor(Colors.Grey.Lighten2)
                    .PaddingTop(7)
                    .Row(row =>
                    {
                        row.RelativeItem().Text($"Generated {MacharPos.Web.Utils.BusinessTime.LocalNow():dd MMM yyyy HH:mm} WAT")
                            .FontSize(7).FontColor(Colors.Grey.Darken1);
                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                    });
            });
        });

        return document.GeneratePdf();
    }

    private static void SummaryCard(
        IContainer container,
        string title,
        decimal amount,
        int count,
        string accentColor)
    {
        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten5)
            .Padding(9)
            .Column(column =>
            {
                column.Item().Text(title).SemiBold().FontColor(accentColor);
                column.Item().Text($"₦{amount:N2}").Bold().FontSize(13);
                column.Item().Text($"{count:N0} record{(count == 1 ? "" : "s")}")
                    .FontSize(7).FontColor(Colors.Grey.Darken1);
            });
    }

    private static IContainer HeaderCell(IContainer container)
        => container.Background(BrandRed).PaddingVertical(6).PaddingHorizontal(4)
            .DefaultTextStyle(style => style.SemiBold().FontColor(Colors.White).FontSize(7.5f));

    private static IContainer BodyCell(IContainer container)
        => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(6).PaddingHorizontal(4);

    private static string BuildFilterDescription(ExpenseReportVm report)
    {
        var station = report.Filter.StationId.HasValue
            ? report.Stations.FirstOrDefault(x => x.Id == report.Filter.StationId.Value)?.Name ?? "Selected station"
            : "All stations";

        var category = report.Filter.Category.HasValue
            ? ExpenseReporting.CategoryName(report.Filter.Category.Value)
            : "All categories";

        var status = report.Filter.Status.HasValue
            ? ExpenseReporting.StatusName(report.Filter.Status.Value)
            : "All statuses";

        var payment = report.Filter.PaymentStatus.HasValue
            ? report.Filter.PaymentStatus.Value.ToString()
            : "All payment states";

        return $"{station} · {category} · {status} · {payment}";
    }
}
