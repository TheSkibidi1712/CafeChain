using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new HomeViewModel
            {
                Categories = await _context.DrinkCategories
                    .Where(c => c.Active)
                    .ToListAsync(),

                Drinks = await _context.Drinks
                    .Include(d => d.DrinkImages)
                    .Include(d => d.DrinkSizes)
                    .Include(d => d.Ratings)
                    //.Include(d => d.Category) // Nạp thêm Category để render theo từng cụm (Nước, Bánh...)
                    .Where(d => d.Active)
                    .Take(6)
                    .ToListAsync()
            };

            return View(viewModel);
        }
    }
}