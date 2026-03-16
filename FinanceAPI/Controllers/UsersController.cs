using System.Security.Claims;
using FinanceAPI.DTOs.ApiKeys;
using FinanceAPI.DTOs.Users;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Interfaces.Services;
using FinanceAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAPI.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepo;

    public UsersController(IUserService userService, IUserRepository userRepo)
    {
        _userService = userService;
        _userRepo = userRepo;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
    {
        return Ok(await _userService.GetAllAsync());
    }

    [HttpGet("{userId:int}")]
    public async Task<ActionResult<UserDto>> GetById(int userId)
    {
        return Ok(await _userService.GetByIdAsync(userId));
    }

    [HttpPut("{userId:int}")]
    public async Task<ActionResult<UserDto>> Update(int userId, [FromBody] UpdateUserRequest request)
    {
        return Ok(await _userService.UpdateAsync(userId, request));
    }

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> Delete(int userId)
    {
        await _userService.DeleteAsync(userId);
        return NoContent();
    }

    // PUT /api/users/{userId}/active
    [HttpPut("{userId:int}/active")]
    public async Task<IActionResult> SetActive(int userId, [FromBody] bool isActive)
    {
        int requestingId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (userId == requestingId)
        {
            throw new InvalidOperationException("Du kannst dein eigenes Konto nicht sperren.");
        }

        await _userService.SetActiveAsync(userId, isActive);
        return NoContent();
    }

    // PUT /api/users/{userId}/password
    [HttpPut("{userId:int}/password")]
    public async Task<IActionResult> SetPassword(int userId, [FromBody] AdminSetPasswordRequest request)
    {
        User user = await _userRepo.GetByIdAsync(userId)
                   ?? throw new KeyNotFoundException($"User {userId} not found.");

        string newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        await _userRepo.UpdatePasswordAsync(user.Id, newHash);

        return NoContent();
    }

    [HttpPost("{userId:int}/apikeys")]
    public async Task<ActionResult<ApiKeyCreatedResponse>> CreateApiKey(int userId, [FromBody] CreateApiKeyRequest request)
    {
        int adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        ApiKeyCreatedResponse result = await _userService.CreateApiKeyAsync(userId, request.Name, createdByAdminId: adminId);
        return Ok(result);
    }

    [HttpGet("{userId:int}/apikeys")]
    public async Task<ActionResult<IEnumerable<ApiKeyDto>>> GetApiKeys(int userId)
    {
        return Ok(await _userService.GetApiKeysAsync(userId));
    }

    [HttpDelete("{userId:int}/apikeys/{keyId:int}")]
    public async Task<IActionResult> RevokeApiKey(int userId, int keyId)
    {
        await _userService.RevokeApiKeyAsync(userId, keyId);
        return NoContent();
    }
}
