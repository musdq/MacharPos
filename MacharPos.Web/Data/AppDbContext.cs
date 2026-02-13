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

        public DbSet<DailyClose> DailyCloses => Set<DailyClose>();

        public DbSet<CreditPayment> CreditPayments => Set<CreditPayment>();

        // Phase 2
        public DbSet<ShiftClose> ShiftCloses => Set<ShiftClose>();



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


            // Helpful indexes
            b.Entity<Sale>().HasIndex(x => new { x.StationId, x.CreatedAt });
            b.Entity<LpgBatch>().HasIndex(x => new { x.StationId, x.Status });
            b.Entity<Product>().HasIndex(x => x.Name);
        }
    }
}
