using Microsoft.AspNetCore.Mvc;

namespace ConfigSystem.Api.Controllers;

/// <summary>
/// Authentication endpoints for IBM i credential management.
/// Stores user credentials in the session for use by IFS operations.
/// </summary>
[ApiController]
[Route("api/auth")]
[Tags("auth")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;

    public AuthController(ILogger<AuthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and stores credentials in the session.
    /// </summary>
    /// <param name="request">Login request with userId and password</param>
    /// <returns>Success status</returns>
    [HttpPost("login")]
    [Produces("application/json")]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Password))
        {
            _logger.LogWarning("Login attempt with missing credentials");
            return BadRequest(new { message = "UserId and Password are required" });
        }

        try
        {
            HttpContext.Session.SetString("uid", request.UserId);
            _logger.LogInformation("User {UserId} logged in successfully", request.UserId);
            
            return Ok(new LoginResponse 
            { 
                Message = "Login successful",
                UserId = request.UserId,
                IsAuthenticated = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user {UserId}", request.UserId);
            return StatusCode(500, new { message = "An error occurred during login" });
        }
    }

    /// <summary>
    /// Logs out the current user and clears session credentials.
    /// </summary>
    [HttpPost("logout")]
    [Produces("application/json")]
    public ActionResult<LogoutResponse> Logout()
    {
        try
        {
            HttpContext.Session.Remove("uid");
            _logger.LogInformation("User logged out");
            
            return Ok(new LogoutResponse { Message = "Logout successful" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { message = "An error occurred during logout" });
        }
    }

    /// <summary>
    /// Checks if the current user is authenticated.
    /// </summary>
    [HttpGet("status")]
    [Produces("application/json")]
    public ActionResult<StatusResponse> GetStatus()
    {
        var userId = HttpContext.Session.GetString("uid");
        var isAuthenticated = !string.IsNullOrEmpty(userId);
        
        return Ok(new StatusResponse 
        { 
            IsAuthenticated = isAuthenticated,
            UserId = userId
        });
    }
}

/// <summary>
/// Request model for login endpoint.
/// </summary>
public class LoginRequest
{
    public string UserId { get; set; } = "";
    public string Password { get; set; } = "";
}

/// <summary>
/// Response model for login endpoint.
/// </summary>
public class LoginResponse
{
    public string Message { get; set; } = "";
    public string UserId { get; set; } = "";
    public bool IsAuthenticated { get; set; }
}

/// <summary>
/// Response model for logout endpoint.
/// </summary>
public class LogoutResponse
{
    public string Message { get; set; } = "";
}

/// <summary>
/// Response model for status endpoint.
/// </summary>
public class StatusResponse
{
    public bool IsAuthenticated { get; set; }
    public string? UserId { get; set; }
}
