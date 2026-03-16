using System.ComponentModel.DataAnnotations;
using FinanceAPI.DTOs.Transactions;
using FinanceAPI.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAPI.Controllers;

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionsController : AuthenticatedControllerBase
{
    private readonly ITransactionService _transactionService;

    public TransactionsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransactionDto>>> GetAll(
        [FromQuery] int? month,
        [FromQuery] int? year,
        [FromQuery] int? categoryId,
        [FromQuery][RegularExpression("^(income|expense)$", ErrorMessage = "type must be 'income' or 'expense'.")] string? type,
        CancellationToken cancellationToken)
    {
        return Ok(await _transactionService.GetAllAsync(UserId, month, year, categoryId, type, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TransactionDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _transactionService.GetByIdAsync(UserId, id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create([FromBody] CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        TransactionDto result = await _transactionService.CreateAsync(UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TransactionDto>> Update(int id, [FromBody] UpdateTransactionRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _transactionService.UpdateAsync(UserId, id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _transactionService.DeleteAsync(UserId, id, cancellationToken);
        return NoContent();
    }
}
