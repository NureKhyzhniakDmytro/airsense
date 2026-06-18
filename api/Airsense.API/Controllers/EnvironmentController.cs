using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Airsense.API.Models.Dto;
using Airsense.API.Models.Dto.Environment;
using Airsense.API.Models.Entity;
using Airsense.API.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Environment = Airsense.API.Models.Entity.Environment;

namespace Airsense.API.Controllers;

[ApiController]
[Route("env")]
[Authorize]
public class EnvironmentController(
    IEnvironmentRepository environmentRepository,
    IUserRepository userRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAvailableEnvironments(
        [FromQuery][Range(0, int.MaxValue, ErrorMessage = "The skip parameter must be a non-negative integer.")] int skip = 0,
        [FromQuery][Range(0, int.MaxValue, ErrorMessage = "The count parameter must be a non-negative integer.")] int count = 10
    )
    {
        var userId = await ResolveCurrentUserIdAsync();
        if (userId is null)
            return BadRequest(new { message = "You are not registered" });

        var environments = await environmentRepository.GetAvailableAsync(userId.Value, count, skip);
        var totalCount = await environmentRepository.CountAvailableAsync(userId.Value);
        
        return Ok(new PaginatedListDto
            {
                Data = environments,
                Pagination = new PaginatedListDto.Metadata
                {
                    Skip = skip,
                    Count = environments.Count,
                    Total = totalCount
                }
            }
        );  
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateEnvironment([FromBody] CreateEnvironmentRequestDto request)
    {
        var userId = await ResolveCurrentUserIdAsync();
        if (userId is null)
            return BadRequest(new { message = "You are not registered" });

        var environment = new Environment
        {
            Name = request.Name,
            Icon = request.Icon
        };
        environment = await environmentRepository.CreateAsync(environment, userId.Value);
        return CreatedAtAction(nameof(GetEnvironment), new { envId = environment.Id }, environment);
    }
    
    [HttpGet("{envId:int}")]
    public async Task<IActionResult> GetEnvironment(int envId)
    {
        var userId = await ResolveCurrentUserIdAsync();
        if (userId is null)
            return BadRequest(new { message = "You are not registered" });

        var environment = await environmentRepository.GetByIdAsync(envId);
        if (environment is null)
            return NotFound(new { message = "Environment not found" });
        
        var role = await environmentRepository.GetRoleAsync(userId.Value, envId);
        if (role is null)
            return Forbid();
        
        return Ok(new EnvironmentDto
        {
            Id = environment.Id,
            Name = environment.Name,
            Icon = environment.Icon,
            Role = role
        });
    }

    [HttpDelete("{envId:int}")]
    public async Task<IActionResult> DeleteEnvironment(int envId)
    {
        var userId = await ResolveCurrentUserIdAsync();
        if (userId is null)
            return BadRequest(new { message = "You are not registered" });

        var environment = await environmentRepository.GetByIdAsync(envId);
        if (environment is null)
            return NotFound(new { message = "Environment not found" });
        
        var role = await environmentRepository.GetRoleAsync(userId.Value, envId);
        if (role is null || !role.Equals("owner"))
            return Forbid();

        await environmentRepository.DeleteAsync(envId);
        return NoContent();
    }

    [HttpPatch("{envId:int}")]
    public async Task<IActionResult> UpdateEnvironment(int envId, [FromBody] UpdateEnvironmentRequestDto request)
    {
        var userId = await ResolveCurrentUserIdAsync();
        if (userId is null)
            return BadRequest(new { message = "You are not registered" });

        var environment = await environmentRepository.GetByIdAsync(envId);
        if (environment is null)
            return NotFound(new { message = "Environment not found" });
        
        var role = await environmentRepository.GetRoleAsync(userId.Value, envId);
        if (role is null || !role.Equals("owner"))
            return Forbid();
        
        await environmentRepository.UpdateAsync(envId, request.Name, request.Icon);
        return NoContent();
    }

    private async Task<int?> ResolveCurrentUserIdAsync()
    {
        var hasTokenUserId = int.TryParse(User.FindFirstValue("id"), out var userId);

        if (hasTokenUserId && await userRepository.IsExistsByIdAsync(userId))
            return userId;

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(email))
            return null;

        var existingUser = await userRepository.GetByUidAsync(uid)
                           ?? await userRepository.GetByEmailAsync(email);
        if (existingUser is not null)
            return existingUser.Id;

        var user = new User
        {
            Id = hasTokenUserId ? userId : 0,
            Uid = uid,
            Name = User.FindFirstValue("name") ?? email,
            Email = email
        };

        user = hasTokenUserId
            ? await userRepository.CreateWithIdAsync(user)
            : await userRepository.CreateAsync(user);

        return user.Id;
    }
}
