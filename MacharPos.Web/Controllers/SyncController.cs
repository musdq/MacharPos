using MacharPos.Core.Entities;
using MacharPos.Web.Data;
using MacharPos.Web.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MacharPos.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SyncController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Sync([FromBody] SyncQueueItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.EntityType))
                return BadRequest("Invalid sync payload.");

            if (item.EntityType == "Sale")
            {
                if (string.IsNullOrWhiteSpace(item.PayloadJson))
                    return BadRequest("Sale payload is missing.");

                var dto = JsonSerializer.Deserialize<SaleSyncDto>(item.PayloadJson);
                if (dto == null)
                    return BadRequest("Unable to deserialize sale payload.");

                var exists = await _context.Sales.AnyAsync(s => s.Id == dto.Id);
                if (exists) return Ok();

                var sale = new Sale
                {
                    Id = dto.Id,
                    CreatedAt = dto.Date,
                    Total = dto.TotalAmount,
                    Subtotal = dto.TotalAmount,
                    Items = dto.Items.Select(i =>
                    {
                        var isLpg = i.Kg > 0;
                        var lineTotal = isLpg
                            ? Math.Round(i.Kg * i.Price, 2, MidpointRounding.AwayFromZero)
                            : Math.Round(i.Cylinders * i.Price, 2, MidpointRounding.AwayFromZero);

                        return new SaleItem
                        {
                            Id = i.Id,
                            ProductId = i.ProductId,
                            Qty = i.Cylinders,
                            LpgKg = i.Kg > 0 ? i.Kg : null,
                            UnitPrice = i.Price,
                            LineTotal = lineTotal
                        };
                    }).ToList()
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();
            }

            return Ok();
        }
    }
}