using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using CentaurScores.Api.Domain;
using CentaurScores.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CentaurScores.Api.Controllers;

[Route("api/categories")]
public sealed class CategoriesController(ApplicationDbContext db, ITenantContext tenantContext) : ApiControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.Categories.AsNoTracking().Include(item => item.Values).Where(item => item.TenantId == TenantId).OrderBy(item => item.Name).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var category = new Category { Id = Guid.NewGuid(), TenantId = TenantId, Name = request.Name };
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"api/categories/{category.Id}", category);
    }

    [HttpPost("{categoryId:guid}/values")]
    public async Task<IActionResult> AddValue(Guid categoryId, CreateCategoryValueRequest request, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var category = await db.Categories.SingleOrDefaultAsync(item => item.Id == categoryId && item.TenantId == TenantId, cancellationToken);
        if (category is null) return NotFound();
        var value = new CategoryValue { CategoryId = categoryId, TenantId = TenantId, ValueId = request.ValueId, Name = request.Name };
        db.CategoryValues.Add(value);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(value);
    }

    [HttpDelete("{categoryId:guid}")]
    public async Task<IActionResult> Delete(Guid categoryId, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return Forbid();
        var category = await db.Categories.SingleOrDefaultAsync(item => item.Id == categoryId && item.TenantId == TenantId, cancellationToken);
        if (category is null) return NotFound();
        if (category.IsUsed) return Conflict(new { message = "A category used by a match cannot be deleted." });
        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{categoryId:guid}/values/{valueId:int}")]
    public async Task<IActionResult> DeleteValue(Guid categoryId, int valueId, CancellationToken cancellationToken)
    {
        if (!CanManage) return Forbid();
        var value = await db.CategoryValues.SingleOrDefaultAsync(item => item.CategoryId == categoryId && item.ValueId == valueId && item.TenantId == TenantId, cancellationToken);
        if (value is null) return NotFound();
        db.CategoryValues.Remove(value);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}