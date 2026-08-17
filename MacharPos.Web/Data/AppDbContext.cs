using MacharPos.Core.Entities;
using MacharPos.Web.Auth;
using MacharPos.Web.ViewModels.Credit;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace MacharPos.Web.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Station> Stations => Set<Station>();
        public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<LpgBatch> LpgBatches => Set<LpgBatch>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Sale> Sales => Set<Sale>();
        public DbSet<SaleItem> SaleItems => Set<SaleItem>();

        public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();

        public DbSet<DailyClose> DailyCloses => Set<DailyClose>();

        public DbSet<CreditPayment> CreditPayments => Set<CreditPayment>();

        // Phase 2
        public DbSet<ShiftClose> ShiftCloses => Set<ShiftClose>();

        public DbSet<SyncQueueItem> SyncQueue => Set<SyncQueueItem>();

        public DbSet<ExpenseRequest> ExpenseRequests => Set<ExpenseRequest>();
        public DbSet<CashMovement> CashMovements => Set<CashMovement>();
        public DbSet<CashReconciliation> CashReconciliations => Set<CashReconciliation>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.Entity<Sale>()
                .HasMany(s => s.Items)
                .WithOne(i => i.Sale!)
                .HasForeignKey(i => i.SaleId);

            b.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany()
                .HasForeignKey(p => p.CategoryId);

            b.Entity<LpgBatch>()
                .HasOne(x => x.Station)
                .WithMany()
                .HasForeignKey(x => x.StationId);

            b.Entity<LpgBatch>()
              .Property(x => x.DateReceived)
              .HasColumnType("timestamp without time zone");

            b.Entity<Sale>()
              .Property(x => x.CreatedAt)
              .HasColumnType("timestamp with time zone");

            b.Entity<Sale>()
              .Property(x => x.VoidedAtUtc)
              .HasColumnType("timestamp with time zone");

            b.Entity<DailyClose>()
              .HasIndex(x => new { x.StationId, x.CloseDate })
              .IsUnique();

            b.Entity<DailyClose>()
              .Property(x => x.CloseDate)
              .HasColumnType("date");

            b.Entity<ShiftClose>()
              .HasIndex(x => new { x.StationId, x.ShiftStartUtc });

            b.Entity<ShiftClose>()
              .Property(x => x.ShiftDate)
              .HasColumnType("date");

            b.Entity<ShiftClose>()
              .Property(x => x.ShiftStartUtc)
              .HasColumnType("timestamp with time zone");

            b.Entity<ShiftClose>()
              .Property(x => x.ShiftEndUtc)
              .HasColumnType("timestamp with time zone");

            b.Entity<Sale>()
              .Property(x => x.CreditDueDate)
              .HasColumnType("date");

            b.Entity<LoyaltyTransaction>()
            .Property(x => x.CreatedAt)
            .HasColumnType("timestamp with time zone");

            b.Entity<LoyaltyTransaction>()
                .HasIndex(x => new { x.StationId, x.CustomerId, x.CreatedAt });

            b.Entity<LoyaltyTransaction>()
                .HasIndex(x => x.SaleId);

            b.Entity<ExpenseRequest>(e =>
            {
                e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
                e.Property(x => x.Reason).HasMaxLength(500);
                e.Property(x => x.RequestedByUserId).HasMaxLength(450);
                e.Property(x => x.ReviewedByUserId).HasMaxLength(450);
                e.Property(x => x.ReviewNote).HasMaxLength(500);
                e.Property(x => x.EvidenceOriginalFileName).HasMaxLength(255);
                e.Property(x => x.EvidenceContentType).HasMaxLength(100);
                e.Property(x => x.ExpenseDateTimeUtc).HasColumnType("timestamp with time zone");
                e.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
                e.Property(x => x.ReviewedAtUtc).HasColumnType("timestamp with time zone");
                e.Property(x => x.PaidAtUtc).HasColumnType("timestamp with time zone");
                e.Property(x => x.PaymentCancelledAtUtc).HasColumnType("timestamp with time zone");
                e.Property(x => x.PaidByUserId).HasMaxLength(450);
                e.Property(x => x.PaymentCancelledByUserId).HasMaxLength(450);
                e.Property(x => x.Payee).HasMaxLength(200);
                e.Property(x => x.PaymentReference).HasMaxLength(200);
                e.Property(x => x.PaymentNote).HasMaxLength(500);
                e.Property(x => x.PaymentEvidenceOriginalFileName).HasMaxLength(255);
                e.Property(x => x.PaymentEvidenceContentType).HasMaxLength(100);

                e.HasOne(x => x.Station)
                    .WithMany()
                    .HasForeignKey(x => x.StationId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => new { x.StationId, x.ExpenseDateTimeUtc });
                e.HasIndex(x => new { x.Status, x.CreatedAtUtc });
                e.HasIndex(x => new { x.PaymentStatus, x.PaidAtUtc });
                e.HasIndex(x => new { x.RequestedByUserId, x.CreatedAtUtc });
            });

            b.Entity<CashMovement>(e =>
            {
                e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
                e.Property(x => x.MainCashImpact).HasColumnType("numeric(18,2)");
                e.Property(x => x.PettyCashImpact).HasColumnType("numeric(18,2)");
                e.Property(x => x.OccurredAtUtc).HasColumnType("timestamp with time zone");
                e.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
                e.Property(x => x.Description).HasMaxLength(500);
                e.Property(x => x.Reference).HasMaxLength(200);
                e.Property(x => x.EnteredByUserId).HasMaxLength(450);

                e.HasOne(x => x.Station).WithMany().HasForeignKey(x => x.StationId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.CounterpartyStation).WithMany().HasForeignKey(x => x.CounterpartyStationId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ReversesMovement).WithMany().HasForeignKey(x => x.ReversesMovementId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => new { x.StationId, x.OccurredAtUtc });
                e.HasIndex(x => x.TransferGroupId);
                e.HasIndex(x => x.ReversesMovementId).IsUnique();
            });

            b.Entity<CashReconciliation>(e =>
            {
                e.Property(x => x.ReconciliationDate).HasColumnType("date");
                e.Property(x => x.Notes).HasMaxLength(1000);
                e.Property(x => x.ReopenReason).HasMaxLength(500);
                e.Property(x => x.ClosedByUserId).HasMaxLength(450);
                e.Property(x => x.ReopenedByUserId).HasMaxLength(450);
                e.Property(x => x.ClosedAtUtc).HasColumnType("timestamp with time zone");
                e.Property(x => x.ReopenedAtUtc).HasColumnType("timestamp with time zone");

                e.HasOne(x => x.Station).WithMany().HasForeignKey(x => x.StationId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.StationId, x.ReconciliationDate }).IsUnique();
            });


            // Helpful indexes
            b.Entity<Sale>().HasIndex(x => new { x.StationId, x.CreatedAt });
            b.Entity<LpgBatch>().HasIndex(x => new { x.StationId, x.Status });
            b.Entity<Product>().HasIndex(x => x.Name);
        }
    }
}
