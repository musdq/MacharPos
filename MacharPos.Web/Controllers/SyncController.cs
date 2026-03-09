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
          
            if (item.EntityType == "Sale")
            {
                var dto = JsonSerializer.Deserialize<SaleSyncDto>(item.PayloadJson);

                var exists = await _context.Sales.AnyAsync(s => s.Id == dto.Id);
                if (exists) return Ok();


                var sale = new Sale
                {
                    Id = dto.Id,
                    CreatedAt = dto.Date,
                    Total = dto.TotalAmount,
                    Items = dto.Items.Select(i => new SaleItem
                    {
                        Id = i.Id,
                        ProductId = i.ProductId,
                        Qty = i.Cylinders,
                        LpgKg= i.Kg,
                        UnitPrice = i.Price
                    }).ToList()
                };

                _context.Sales.Add(sale);
                
                await _context.SaveChangesAsync();
            }

            return Ok();
        }
    }
}
