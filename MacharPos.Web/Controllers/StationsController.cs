using MacharPos.Core.Entities;
using MacharPos.Web.Data;
using MacharPos.Web.ViewModels.Stations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MacharPos.Web.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class StationsController : Controller
    {
        private readonly AppDbContext _db;

        public StationsController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var stations = await _db.Stations
                .OrderBy(s => s.Name)
                .ToListAsync();

            return View(stations);
        }

        public IActionResult Create()
        {
            return View(new StationVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StationVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var station = new Station
            {
                Name = vm.Name,
                Location = vm.Location,
                IsActive = vm.IsActive
            };

            _db.Stations.Add(station);
            await _db.SaveChangesAsync();

            TempData["Ok"] = "Station created successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var station = await _db.Stations.FindAsync(id);
            if (station == null) return NotFound();

            var vm = new StationVm
            {
                Id = station.Id,
                Name = station.Name,
                Location = station.Location,
                IsActive = station.IsActive
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(StationVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var station = await _db.Stations.FindAsync(vm.Id);
            if (station == null) return NotFound();

            station.Name = vm.Name;
            station.Location = vm.Location;
            station.IsActive = vm.IsActive;

            await _db.SaveChangesAsync();

            TempData["Ok"] = "Station updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var station = await _db.Stations.FindAsync(id);
            if (station == null) return NotFound();

            _db.Stations.Remove(station);
            await _db.SaveChangesAsync();

            TempData["Ok"] = "Station deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
