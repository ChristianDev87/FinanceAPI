using System.Security.Claims;
using FinanceAPI.DTOs.ApiKeys;
using FinanceAPI.DTOs.Profile;
using FinanceAPI.DTOs.Users;
using FinanceAPI.Interfaces.Repositories;
using FinanceAPI.Interfaces.Services;
using FinanceAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAPI.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepo;

    public ProfileController(IUserService userService, IUserRepository userRepo)
    {
        _userService = userService;
        _userRepo = userRepo;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/profile
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        UserDto dto = await _userService.GetByIdAsync(GetUserId());
        return Ok(dto);
    }

    // PUT /api/profile
    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        UserDto current = await _userService.GetByIdAsync(GetUserId());

        UpdateUserRequest updateRequest = new UpdateUserRequest
        {
            Username = request.Username,
            Email = request.Email,
            Role = current.Role   // role cannot be changed via profile
        };

        UserDto dto = await _userService.UpdateAsync(GetUserId(), updateRequest);
        return Ok(dto);
    }

    // PUT /api/profile/password
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        User user = await _userRepo.GetByIdAsync(GetUserId())
                   ?? throw new KeyNotFoundException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Aktuelles Passwort ist falsch.");
        }

        string newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        await _userRepo.UpdatePasswordAsync(user.Id, newHash);

        return NoContent();
    }

    // DELETE /api/profile
    [HttpDelete]
    public async Task<IActionResult> DeleteAccount()
    {
        await _userService.DeleteAsync(GetUserId());
        return NoContent();
    }

    // GET /api/profile/apikeys
    [HttpGet("apikeys")]
    public async Task<ActionResult<IEnumerable<ApiKeyDto>>> GetApiKeys()
    {
        return Ok(await _userService.GetApiKeysAsync(GetUserId()));
    }

    // POST /api/profile/apikeys
    [HttpPost("apikeys")]
    public async Task<ActionResult<ApiKeyCreatedResponse>> CreateApiKey([FromBody] CreateApiKeyRequest request)
    {
        ApiKeyCreatedResponse result = await _userService.CreateApiKeyAsync(GetUserId(), request.Name);
        return Ok(result);
    }

    // DELETE /api/profile/apikeys/{keyId}
    [HttpDelete("apikeys/{keyId:int}")]
    public async Task<IActionResult> RevokeApiKey(int keyId)
    {
        await _userService.RevokeApiKeyAsync(GetUserId(), keyId);
        return NoContent();
    }
}
