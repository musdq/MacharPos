using MacharPos.Core.Entities;
using MacharPos.Web.Auth;
using MacharPos.Web.Data;
using MacharPos.Web.Services;
using MacharPos.Web.Services.Expenses;
using MacharPos.Web.Utils;
using MacharPos.Web.ViewModels.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Controllers;

[Authorize]
public class ExpensesController : Controller
{
    private readonly AppDbContext _db;
    private readonly IStationContext _stationContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ExpenseEvidenceService _evidenceService;
    private readonly IExpenseReportPdfService _pdfService;

    public ExpensesController(
        AppDbContext db,
        IStationContext stationContext,
        UserManager<ApplicationUser> userManager,
        ExpenseEvidenceService evidenceService,
        IExpenseReportPdfService pdfService)
    {
        _db = db;
        _stationContext = stationContext;
        _userManager = userManager;
        _evidenceService = evidenceService;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> RequestExpense()
    {
        var userId = GetCurrentUserId();
        var form = new ExpenseRequestCreateVm
        {
            ExpenseDateTimeLocal = BusinessTime.LocalNow()
        };

        return View(await BuildRequestPageAsync(userId, form));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> RequestExpense(
        [Bind(Prefix = "Form")] ExpenseRequestCreateVm form,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (form.EvidenceFile is not null && form.EvidencePhoto is not null)
            ModelState.AddModelError("Form.EvidenceFile", "Choose either an uploaded file or a camera photo, not both.");

        ExpenseEvidenceResult? evidence = null;
        var selectedEvidence = form.EvidenceFile ?? form.EvidencePhoto;
        if (selectedEvidence is not null)
        {
            evidence = await _evidenceService.ValidateAndReadAsync(selectedEvidence, cancellationToken);
            if (!evidence.IsValid)
                ModelState.AddModelError("Form.EvidenceFile", evidence.Error ?? "The evidence file is invalid.");
        }

        if (!ModelState.IsValid)
            return View(await BuildRequestPageAsync(userId, form));

        var stationId = await _stationContext.GetStationIdOrThrowAsync();
        var request = new ExpenseRequest
        {
            StationId = stationId,
            RequestedByUserId = userId,
            ExpenseDateTimeUtc = BusinessTime.ToUtc(form.ExpenseDateTimeLocal),
            Reason = form.Reason.Trim(),
            Amount = form.Amount,
            Category = form.Category,
            Status = ExpenseStatus.Pending,
            EvidenceOriginalFileName = evidence?.FileName,
            EvidenceContentType = evidence?.ContentType,
            EvidenceFileSize = evidence?.Data?.LongLength,
            EvidenceData = evidence?.Data,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.ExpenseRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Ok"] = "Expense request submitted for approval.";
        return RedirectToAction(nameof(RequestExpense));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Manage(
        ExpenseStatus? status = ExpenseStatus.Pending,
        Guid? stationId = null,
        int page = 1,
        int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var baseQuery = _db.ExpenseRequests.AsNoTracking();
        var statusSummary = await baseQuery
            .GroupBy(x => x.Status)
            .Select(group => new ExpenseStatusSummary
            {
                Status = group.Key,
                Count = group.Count(),
                Amount = group.Sum(x => x.Amount)
            })
            .ToListAsync();
        var paymentSummary = await baseQuery
            .Where(x => x.Status == ExpenseStatus.Approved)
            .GroupBy(x => x.PaymentStatus)
            .Select(group => new ExpensePaymentSummary
            {
                Status = group.Key,
                Count = group.Count(),
                Amount = group.Sum(x => x.Amount)
            })
            .ToListAsync();

        IQueryable<ExpenseRequest> filtered = baseQuery;
        if (status.HasValue)
            filtered = filtered.Where(x => x.Status == status.Value);
        if (stationId.HasValue)
            filtered = filtered.Where(x => x.StationId == stationId.Value);

        var totalCount = await filtered.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (page > totalPages)
            page = totalPages;

        var pageQuery = filtered
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        var vm = new ExpenseManagementVm
        {
            Items = await LoadRowsAsync(pageQuery),
            Stations = await LoadStationsAsync(),
            StatusFilter = status,
            StationId = stationId,
            PendingCount = SummaryCount(statusSummary, ExpenseStatus.Pending),
            ApprovedCount = SummaryCount(statusSummary, ExpenseStatus.Approved),
            RejectedCount = SummaryCount(statusSummary, ExpenseStatus.Rejected),
            PendingAmount = SummaryAmount(statusSummary, ExpenseStatus.Pending),
            ApprovedUnpaidCount = PaymentSummaryCount(paymentSummary, ExpensePaymentStatus.Unpaid),
            ApprovedUnpaidAmount = PaymentSummaryAmount(paymentSummary, ExpensePaymentStatus.Unpaid),
            PaidCount = PaymentSummaryCount(paymentSummary, ExpensePaymentStatus.Paid),
            PaidAmount = PaymentSummaryAmount(paymentSummary, ExpensePaymentStatus.Paid),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return View(vm);

        static int SummaryCount(IEnumerable<ExpenseStatusSummary> summary, ExpenseStatus target)
            => summary.Where(x => x.Status == target).Select(x => x.Count).FirstOrDefault();

        static decimal SummaryAmount(IEnumerable<ExpenseStatusSummary> summary, ExpenseStatus target)
            => summary.Where(x => x.Status == target).Select(x => x.Amount).FirstOrDefault();

        static int PaymentSummaryCount(IEnumerable<ExpensePaymentSummary> summary, ExpensePaymentStatus target)
            => summary.Where(x => x.Status == target).Select(x => x.Count).FirstOrDefault();

        static decimal PaymentSummaryAmount(IEnumerable<ExpensePaymentSummary> summary, ExpensePaymentStatus target)
            => summary.Where(x => x.Status == target).Select(x => x.Amount).FirstOrDefault();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public Task<IActionResult> Approve(
        Guid id,
        string? reviewNote,
        ExpenseStatus? returnStatus,
        Guid? returnStationId,
        int returnPage = 1)
        => ReviewAsync(id, ExpenseStatus.Approved, reviewNote, returnStatus, returnStationId, returnPage);

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public Task<IActionResult> Reject(
        Guid id,
        string? reviewNote,
        ExpenseStatus? returnStatus,
        Guid? returnStationId,
        int returnPage = 1)
        => ReviewAsync(id, ExpenseStatus.Rejected, reviewNote, returnStatus, returnStationId, returnPage);

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Pay(
        ExpensePaymentVm form,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(form.PaymentMethod))
            ModelState.AddModelError(nameof(form.PaymentMethod), "Choose a valid payment method.");

        ExpenseEvidenceResult? evidence = null;
        if (form.PaymentEvidence is not null)
        {
            evidence = await _evidenceService.ValidateAndReadAsync(form.PaymentEvidence, cancellationToken);
            if (!evidence.IsValid)
                ModelState.AddModelError(nameof(form.PaymentEvidence), evidence.Error ?? "The payment proof is invalid.");
        }

        if (!ModelState.IsValid)
        {
            TempData["Err"] = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage));
            return ManageRedirect(form.ReturnStatus, form.ReturnStationId, form.ReturnPage);
        }

        var paidAt = DateTime.UtcNow;
        var paymentReference = ExpensePaymentReferenceGenerator.Generate(paidAt);
        var userId = GetCurrentUserId();
        var updated = await _db.ExpenseRequests
            .Where(x => x.Id == form.Id &&
                        x.Status == ExpenseStatus.Approved &&
                        x.PaymentStatus == ExpensePaymentStatus.Unpaid)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.PaymentStatus, ExpensePaymentStatus.Paid)
                .SetProperty(x => x.PaymentMethod, form.PaymentMethod)
                .SetProperty(x => x.PaidAtUtc, paidAt)
                .SetProperty(x => x.PaidByUserId, userId)
                .SetProperty(x => x.Payee, form.Payee.Trim())
                .SetProperty(x => x.PaymentReference, paymentReference)
                .SetProperty(x => x.PaymentNote, string.IsNullOrWhiteSpace(form.PaymentNote) ? null : form.PaymentNote.Trim())
                .SetProperty(x => x.PaymentEvidenceOriginalFileName, evidence == null ? null : evidence.FileName)
                .SetProperty(x => x.PaymentEvidenceContentType, evidence == null ? null : evidence.ContentType)
                .SetProperty(x => x.PaymentEvidenceFileSize, evidence == null ? null : evidence.Data!.LongLength)
                .SetProperty(x => x.PaymentEvidenceData, evidence == null ? null : evidence.Data),
                cancellationToken);

        TempData[updated == 1 ? "Ok" : "Err"] = updated == 1
            ? "Expense marked as paid and added to the financial records."
            : "This expense is not approved and unpaid, or it was updated by another admin.";
        return ManageRedirect(form.ReturnStatus, form.ReturnStationId, form.ReturnPage);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CancelPayment(
        Guid id,
        string reason,
        ExpenseStatus? returnStatus,
        Guid? returnStationId,
        int returnPage = 1)
    {
        reason = reason?.Trim() ?? "";
        if (reason.Length < 3 || reason.Length > 500)
        {
            TempData["Err"] = "A cancellation reason between 3 and 500 characters is required.";
            return ManageRedirect(returnStatus, returnStationId, returnPage);
        }

        var updated = await _db.ExpenseRequests
            .Where(x => x.Id == id &&
                        x.Status == ExpenseStatus.Approved &&
                        x.PaymentStatus == ExpensePaymentStatus.Unpaid)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.PaymentStatus, ExpensePaymentStatus.Cancelled)
                .SetProperty(x => x.PaymentCancelledAtUtc, DateTime.UtcNow)
                .SetProperty(x => x.PaymentCancelledByUserId, GetCurrentUserId())
                .SetProperty(x => x.PaymentNote, reason));

        TempData[updated == 1 ? "Ok" : "Err"] = updated == 1
            ? "The approved expense payment was cancelled."
            : "Only an approved, unpaid expense can be cancelled.";
        return ManageRedirect(returnStatus, returnStationId, returnPage);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Report([FromQuery] ExpenseReportFilterVm filter)
        => View(await BuildReportAsync(filter));

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportPdf([FromQuery] ExpenseReportFilterVm filter)
    {
        var report = await BuildReportAsync(filter);
        var pdf = _pdfService.Generate(report);
        var fileName = $"Machar_Expense_Report_{report.RangeStartLocal:yyyyMMdd}_{report.RangeEndLocal:yyyyMMdd}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> Evidence(Guid id)
    {
        var userId = GetCurrentUserId();
        var evidence = await _db.ExpenseRequests.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.RequestedByUserId,
                x.EvidenceData,
                x.EvidenceContentType,
                x.EvidenceOriginalFileName
            })
            .FirstOrDefaultAsync();

        if (evidence is null || evidence.EvidenceData is null ||
            string.IsNullOrWhiteSpace(evidence.EvidenceContentType) ||
            string.IsNullOrWhiteSpace(evidence.EvidenceOriginalFileName))
        {
            return NotFound();
        }

        if (!User.IsInRole("Admin") && evidence.RequestedByUserId != userId)
            return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(evidence.EvidenceData, evidence.EvidenceContentType, evidence.EvidenceOriginalFileName);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PaymentEvidence(Guid id)
    {
        var evidence = await _db.ExpenseRequests.AsNoTracking()
            .Where(x => x.Id == id && x.PaymentStatus == ExpensePaymentStatus.Paid)
            .Select(x => new
            {
                x.PaymentEvidenceData,
                x.PaymentEvidenceContentType,
                x.PaymentEvidenceOriginalFileName
            })
            .FirstOrDefaultAsync();

        if (evidence?.PaymentEvidenceData is null ||
            string.IsNullOrWhiteSpace(evidence.PaymentEvidenceContentType) ||
            string.IsNullOrWhiteSpace(evidence.PaymentEvidenceOriginalFileName))
            return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(evidence.PaymentEvidenceData, evidence.PaymentEvidenceContentType,
            evidence.PaymentEvidenceOriginalFileName);
    }

    private async Task<IActionResult> ReviewAsync(
        Guid id,
        ExpenseStatus newStatus,
        string? reviewNote,
        ExpenseStatus? returnStatus,
        Guid? returnStationId,
        int returnPage)
    {
        reviewNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim();
        if (reviewNote?.Length > 500)
        {
            TempData["Err"] = "The approval/rejection note must be 500 characters or fewer.";
            return RedirectToAction(nameof(Manage), new { status = returnStatus, stationId = returnStationId, page = returnPage });
        }

        var userId = GetCurrentUserId();
        var reviewedAt = DateTime.UtcNow;

        var updated = await _db.ExpenseRequests
            .Where(x => x.Id == id && x.Status == ExpenseStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, newStatus)
                .SetProperty(x => x.ReviewedByUserId, userId)
                .SetProperty(x => x.ReviewedAtUtc, reviewedAt)
                .SetProperty(x => x.ReviewNote, reviewNote));

        if (updated == 0)
            TempData["Err"] = "This request no longer exists or has already been reviewed.";
        else
            TempData["Ok"] = newStatus == ExpenseStatus.Approved
                ? "Expense request approved."
                : "Expense request rejected.";

        return RedirectToAction(nameof(Manage), new { status = returnStatus, stationId = returnStationId, page = returnPage });
    }

    private async Task<ExpenseRequestPageVm> BuildRequestPageAsync(
        string userId,
        ExpenseRequestCreateVm form)
    {
        var items = await LoadRowsAsync(_db.ExpenseRequests.AsNoTracking()
            .Where(x => x.RequestedByUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100));

        return new ExpenseRequestPageVm
        {
            Form = form,
            MyRequests = items
        };
    }

    private async Task<ExpenseReportVm> BuildReportAsync(ExpenseReportFilterVm filter)
    {
        var range = ExpenseReporting.ResolveRange(filter.Period, filter.StartDate, filter.EndDate);
        filter.Period = range.Period;
        filter.StartDate = range.StartLocal;
        filter.EndDate = range.EndLocal;

        IQueryable<ExpenseRequest> query = _db.ExpenseRequests.AsNoTracking()
            .Where(x => x.ExpenseDateTimeUtc >= range.UtcStart &&
                        x.ExpenseDateTimeUtc < range.UtcEndExclusive);

        if (filter.StationId.HasValue)
            query = query.Where(x => x.StationId == filter.StationId.Value);
        if (filter.Category.HasValue)
            query = query.Where(x => x.Category == filter.Category.Value);
        if (filter.Status.HasValue)
            query = query.Where(x => x.Status == filter.Status.Value);
        if (filter.PaymentStatus.HasValue)
            query = query.Where(x => x.Status == ExpenseStatus.Approved &&
                                     x.PaymentStatus == filter.PaymentStatus.Value);

        var items = await LoadRowsAsync(query.OrderByDescending(x => x.ExpenseDateTimeUtc));

        return new ExpenseReportVm
        {
            Filter = filter,
            Items = items,
            Stations = await LoadStationsAsync(),
            RangeStartLocal = range.StartLocal,
            RangeEndLocal = range.EndLocal,
            PeriodLabel = range.Label,
            ApprovedAmount = items.Where(x => x.Status == ExpenseStatus.Approved).Sum(x => x.Amount),
            PendingAmount = items.Where(x => x.Status == ExpenseStatus.Pending).Sum(x => x.Amount),
            RejectedAmount = items.Where(x => x.Status == ExpenseStatus.Rejected).Sum(x => x.Amount),
            ApprovedCount = items.Count(x => x.Status == ExpenseStatus.Approved),
            PendingCount = items.Count(x => x.Status == ExpenseStatus.Pending),
            RejectedCount = items.Count(x => x.Status == ExpenseStatus.Rejected)
            ,PaidAmount = items.Where(x => x.Status == ExpenseStatus.Approved && x.PaymentStatus == ExpensePaymentStatus.Paid).Sum(x => x.Amount)
            ,PaidCount = items.Count(x => x.Status == ExpenseStatus.Approved && x.PaymentStatus == ExpensePaymentStatus.Paid)
            ,ApprovedUnpaidAmount = items.Where(x => x.Status == ExpenseStatus.Approved && x.PaymentStatus == ExpensePaymentStatus.Unpaid).Sum(x => x.Amount)
            ,ApprovedUnpaidCount = items.Count(x => x.Status == ExpenseStatus.Approved && x.PaymentStatus == ExpensePaymentStatus.Unpaid)
        };
    }

    private async Task<List<ExpenseListRowVm>> LoadRowsAsync(IQueryable<ExpenseRequest> query)
    {
        var raw = await query.Select(x => new ExpenseRowData
        {
            Id = x.Id,
            ExpenseDateTimeUtc = x.ExpenseDateTimeUtc,
            CreatedAtUtc = x.CreatedAtUtc,
            Reason = x.Reason,
            Amount = x.Amount,
            Category = x.Category,
            Status = x.Status,
            StationName = x.Station != null ? x.Station.Name : "",
            RequestedByUserId = x.RequestedByUserId,
            HasEvidence = x.EvidenceData != null,
            ReviewedByUserId = x.ReviewedByUserId,
            ReviewedAtUtc = x.ReviewedAtUtc,
            ReviewNote = x.ReviewNote
            ,PaymentStatus = x.PaymentStatus
            ,PaymentMethod = x.PaymentMethod
            ,PaidAtUtc = x.PaidAtUtc
            ,PaidByUserId = x.PaidByUserId
            ,Payee = x.Payee
            ,PaymentReference = x.PaymentReference
            ,PaymentNote = x.PaymentNote
            ,HasPaymentEvidence = x.PaymentEvidenceData != null
        }).ToListAsync();

        var userIds = raw.Select(x => x.RequestedByUserId)
            .Concat(raw.Where(x => x.ReviewedByUserId != null).Select(x => x.ReviewedByUserId!))
            .Concat(raw.Where(x => x.PaidByUserId != null).Select(x => x.PaidByUserId!))
            .Distinct()
            .ToList();
        var userNames = await _db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Email ?? x.UserName ?? x.Id);

        return raw.Select(x => new ExpenseListRowVm
        {
            Id = x.Id,
            ExpenseDateTimeLocal = BusinessTime.ToLocal(x.ExpenseDateTimeUtc),
            CreatedAtLocal = BusinessTime.ToLocal(x.CreatedAtUtc),
            Reason = x.Reason,
            Amount = x.Amount,
            Category = x.Category,
            Status = x.Status,
            StationName = x.StationName,
            RequestedByUserId = x.RequestedByUserId,
            RequesterName = userNames.GetValueOrDefault(x.RequestedByUserId, x.RequestedByUserId),
            HasEvidence = x.HasEvidence,
            ReviewedByUserId = x.ReviewedByUserId,
            ReviewerName = x.ReviewedByUserId is null
                ? null
                : userNames.GetValueOrDefault(x.ReviewedByUserId, x.ReviewedByUserId),
            ReviewedAtLocal = x.ReviewedAtUtc.HasValue ? BusinessTime.ToLocal(x.ReviewedAtUtc.Value) : null,
            ReviewNote = x.ReviewNote
            ,PaymentStatus = x.PaymentStatus
            ,PaymentMethod = x.PaymentMethod
            ,PaidAtLocal = x.PaidAtUtc.HasValue ? BusinessTime.ToLocal(x.PaidAtUtc.Value) : null
            ,PayerName = x.PaidByUserId is null ? null : userNames.GetValueOrDefault(x.PaidByUserId, x.PaidByUserId)
            ,Payee = x.Payee
            ,PaymentReference = x.PaymentReference
            ,PaymentNote = x.PaymentNote
            ,HasPaymentEvidence = x.HasPaymentEvidence
        }).ToList();
    }

    private IActionResult ManageRedirect(ExpenseStatus? status, Guid? stationId, int page)
        => RedirectToAction(nameof(Manage), new { status, stationId, page });

    private Task<List<ExpenseStationOption>> LoadStationsAsync()
        => _db.Stations.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ExpenseStationOption(x.Id, x.Name))
            .ToListAsync();

    private string GetCurrentUserId()
        => _userManager.GetUserId(User)
           ?? throw new InvalidOperationException("The signed-in user could not be identified.");

    private sealed class ExpenseRowData
    {
        public Guid Id { get; set; }
        public DateTime ExpenseDateTimeUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string Reason { get; set; } = "";
        public decimal Amount { get; set; }
        public ExpenseCategory Category { get; set; }
        public ExpenseStatus Status { get; set; }
        public string StationName { get; set; } = "";
        public string RequestedByUserId { get; set; } = "";
        public bool HasEvidence { get; set; }
        public string? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public string? ReviewNote { get; set; }
        public ExpensePaymentStatus PaymentStatus { get; set; }
        public ExpensePaymentMethod? PaymentMethod { get; set; }
        public DateTime? PaidAtUtc { get; set; }
        public string? PaidByUserId { get; set; }
        public string? Payee { get; set; }
        public string? PaymentReference { get; set; }
        public string? PaymentNote { get; set; }
        public bool HasPaymentEvidence { get; set; }
    }

    private sealed class ExpenseStatusSummary
    {
        public ExpenseStatus Status { get; set; }
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class ExpensePaymentSummary
    {
        public ExpensePaymentStatus Status { get; set; }
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }
}
