using FinanceAPI.DTOs.Categories;
using FinanceAPI.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAPI.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : AuthenticatedControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _categoryService.GetAllAsync(UserId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        CategoryDto result = await _categoryService.CreateAsync(UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), null, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Update(int id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _categoryService.UpdateAsync(UserId, id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _categoryService.DeleteAsync(UserId, id, cancellationToken);
        return NoContent();
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] ReorderCategoriesRequest request, CancellationToken cancellationToken)
    {
        await _categoryService.ReorderAsync(UserId, request, cancellationToken);
        return NoContent();
    }
}
