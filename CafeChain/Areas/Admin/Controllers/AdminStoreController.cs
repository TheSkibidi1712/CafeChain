using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Stores;
using CafeChain.ViewModels.Admin.Stores;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers;

[RequirePermission(PermissionConstants.StoreView)]
public sealed class AdminStoreController : AdminBaseController
{
    private readonly IAdminStoreService _service;
    public AdminStoreController(IAdminStoreService service) => _service = service;

    public async Task<IActionResult> Index() => View(await _service.GetAllAsync(User));

    [RequirePermission(PermissionConstants.StoreCreate)]
    public async Task<IActionResult> Create()
    {
        var form = await _service.GetCreateFormAsync();
        ViewBag.Provinces = form.Provinces;
        return View(form.Store);
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionConstants.StoreCreate)]
    public async Task<IActionResult> Create(AdminStoreFormVM model)
    {
        if (ModelState.IsValid)
        {
        var result = await _service.CreateAsync(model, User);
            if (result.IsSuccess) return RedirectToAction(nameof(Index));
            ModelState.AddModelError(string.Empty, result.Message);
        }
        var form = await _service.GetCreateFormAsync();
        ViewBag.Provinces = form.Provinces;
        return View(model);
    }

    [RequirePermission(PermissionConstants.StoreUpdate)]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var form = await _service.GetEditFormAsync(id, User);
            if (form == null) return NotFound();
            ViewBag.Provinces = form.Provinces;
            return View(form.Store);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionConstants.StoreUpdate)]
    public async Task<IActionResult> Edit(int id, AdminStoreFormVM model)
    {
        if (id != model.StoreId) return BadRequest();
        try
        {
            if (ModelState.IsValid)
            {
                var result = await _service.UpdateAsync(model, User);
                if (result.IsSuccess) return RedirectToAction(nameof(Index));
                ModelState.AddModelError(string.Empty, result.Message);
            }
            var form = await _service.GetEditFormAsync(id, User);
            if (form == null) return NotFound();
            ViewBag.Provinces = form.Provinces;
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionConstants.StoreToggleStatus)]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        try
        {
            await _service.ToggleStatusAsync(id, User);
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
