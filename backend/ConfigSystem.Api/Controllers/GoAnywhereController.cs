using ConfigSystem.Api.Data;
using ConfigSystem.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConfigSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GoAnywhereController : ControllerBase
{
    private readonly ConfigDbContext _context;
    private readonly ILogger<GoAnywhereController> _logger;

    public GoAnywhereController(ConfigDbContext context, ILogger<GoAnywhereController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Helper method to check if a configuration is access-controlled (FCB /production)
    /// </summary>
    private bool IsProductionConfigProtected(string? source, string? projectPath)
    {
        return source == "FCB" && !string.IsNullOrEmpty(projectPath) && projectPath.StartsWith("/production");
    }

    /// <summary>
    /// Helper method to get the config source from request header
    /// </summary>
    private string GetConfigSource()
    {
        Request.Headers.TryGetValue("X-Config-Source", out var source);
        return source.ToString() ?? "Dev";
    }

    /// <summary>
    /// GET /api/goanywhere/projects
    /// Retrieves all GoAnywhere projects
    /// </summary>
    [HttpGet("projects")]
    public async Task<ActionResult<IEnumerable<GoAnywhereProjectDto>>> GetProjects()
    {
        try
        {
            var projects = await _context.GoAnywhereProjects
                .OrderBy(p => p.Name)
                .Select(p => new GoAnywhereProjectDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    ProjectPath = p.ProjectPath,
                    ExtentName = p.ExtentName
                })
                .ToListAsync();

            _logger.LogInformation($"Retrieved {projects.Count} GoAnywhere projects");
            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving GoAnywhere projects");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/goanywhere/projects
    /// Creates a new GoAnywhere project
    /// </summary>
    [HttpPost("projects")]
    public async Task<ActionResult<GoAnywhereProjectDto>> CreateProject(
        [FromBody] CreateProjectRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { error = "Project name is required" });

            // Check if project already exists
            var existingProject = await _context.GoAnywhereProjects
                .FirstOrDefaultAsync(p => p.Name == request.Name);
            
            if (existingProject != null)
                return BadRequest(new { error = $"Project '{request.Name}' already exists" });

            var project = new GoAnywhereProject
            {
                Name = request.Name,
                Description = request.Description,
                ProjectPath = request.ProjectPath,
                ContextId = request.ContextId,
                ExtentId = request.ExtentId,
                ExtentName = request.ExtentName,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            _context.GoAnywhereProjects.Add(project);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created new GoAnywhere project {project.Id}: {request.Name}");

            return CreatedAtAction(nameof(GetProjects), new { id = project.Id }, 
                new GoAnywhereProjectDto
                {
                    Id = project.Id,
                    Name = project.Name,
                    Description = project.Description,
                    ProjectPath = project.ProjectPath,
                    ExtentName = project.ExtentName
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating GoAnywhere project");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/goanywhere/configs?projectId=1&environment=DEV
    /// Retrieves configuration for a specific project and environment
    /// </summary>
    [HttpGet("configs")]
    public async Task<ActionResult<IEnumerable<GoAnywhereConfigDto>>> GetConfigurations(
        [FromQuery] int projectId,
        [FromQuery] string environment = "DEV")
    {
        try
        {
            if (projectId <= 0)
                return BadRequest(new { error = "projectId must be greater than 0" });

            var configs = await _context.GoAnywhereConfigs
                .Where(c => c.ProjectId == projectId && c.Environment == environment)
                .OrderBy(c => c.ConfigKey)
                .Select(c => new GoAnywhereConfigDto
                {
                    Id = c.Id,
                    ConfigKey = c.ConfigKey,
                    ConfigValue = c.IsSensitive ? "●●●●●●●●" : c.ConfigValue,
                    Description = c.Description,
                    IsRequired = c.IsRequired,
                    IsSensitive = c.IsSensitive
                })
                .ToListAsync();

            _logger.LogInformation($"Retrieved {configs.Count} configurations for project {projectId}, environment {environment}");
            return Ok(configs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving configurations for project {projectId}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/goanywhere/configs
    /// Creates a new configuration for a project and environment
    /// </summary>
    [HttpPost("configs")]
    public async Task<ActionResult<GoAnywhereConfigDto>> CreateConfiguration(
        [FromBody] CreateConfigurationRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ConfigKey))
                return BadRequest(new { error = "ConfigKey is required" });

            if (request.ProjectId <= 0)
                return BadRequest(new { error = "ProjectId must be greater than 0" });

            if (string.IsNullOrWhiteSpace(request.Environment))
                return BadRequest(new { error = "Environment is required" });

            // Check if project exists
            var project = await _context.GoAnywhereProjects.FindAsync(request.ProjectId);
            if (project == null)
                return NotFound(new { error = $"Project with ID {request.ProjectId} not found" });

            // Check access control: FCB /production configurations are read-only
            var source = GetConfigSource();
            if (IsProductionConfigProtected(source, project.ProjectPath))
            {
                _logger.LogWarning($"Access denied: Attempted to create config in FCB /production project {request.ProjectId} from source {source}");
                return Forbid("FCB /production configurations are read-only and cannot be modified");
            }

            // Check if configuration already exists (UNIQUE constraint)
            var existingConfig = await _context.GoAnywhereConfigs
                .FirstOrDefaultAsync(c => c.ProjectId == request.ProjectId 
                    && c.Environment == request.Environment 
                    && c.ConfigKey == request.ConfigKey);
            
            if (existingConfig != null)
                return BadRequest(new { error = $"Configuration already exists for {request.ConfigKey} in {request.Environment}" });

            var config = new GoAnywhereConfig
            {
                ProjectId = request.ProjectId,
                Environment = request.Environment,
                ConfigKey = request.ConfigKey,
                ConfigValue = request.ConfigValue,
                Description = request.Description,
                IsRequired = request.IsRequired ?? false,
                IsSensitive = request.IsSensitive ?? false,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            _context.GoAnywhereConfigs.Add(config);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created configuration {config.Id}: {request.ConfigKey} for project {request.ProjectId}");

            return CreatedAtAction(nameof(GetConfigurations), new { projectId = request.ProjectId, environment = request.Environment }, 
                new GoAnywhereConfigDto
                {
                    Id = config.Id,
                    ConfigKey = config.ConfigKey,
                    ConfigValue = config.IsSensitive ? "●●●●●●●●" : config.ConfigValue,
                    Description = config.Description,
                    IsRequired = config.IsRequired,
                    IsSensitive = config.IsSensitive
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating configuration");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// PUT /api/goanywhere/configs/{id}
    /// Updates a configuration value
    /// </summary>
    [HttpPut("configs/{id}")]
    public async Task<ActionResult<GoAnywhereConfigDto>> UpdateConfiguration(
        int id,
        [FromBody] UpdateConfigurationRequest request)
    {
        try
        {
            var config = await _context.GoAnywhereConfigs.FindAsync(id);
            if (config == null)
                return NotFound(new { error = $"Configuration with ID {id} not found" });

            // Get the project to check its path
            var project = await _context.GoAnywhereProjects.FindAsync(config.ProjectId);
            if (project == null)
                return NotFound(new { error = $"Project with ID {config.ProjectId} not found" });

            // Check access control: FCB /production configurations are read-only
            var source = GetConfigSource();
            if (IsProductionConfigProtected(source, project.ProjectPath))
            {
                _logger.LogWarning($"Access denied: Attempted to update FCB /production config {id} from source {source}");
                return Forbid("FCB /production configurations are read-only and cannot be modified");
            }

            config.ConfigValue = request.ConfigValue;
            config.LastModifiedDate = DateTime.UtcNow;

            _context.GoAnywhereConfigs.Update(config);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Updated configuration {id}: {config.ConfigKey} = {request.ConfigValue}");

            return Ok(new GoAnywhereConfigDto
            {
                Id = config.Id,
                ConfigKey = config.ConfigKey,
                ConfigValue = config.IsSensitive ? "●●●●●●●●" : config.ConfigValue,
                Description = config.Description,
                IsRequired = config.IsRequired,
                IsSensitive = config.IsSensitive
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating configuration {id}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// DELETE /api/goanywhere/configs/{id}
    /// Deletes a configuration
    /// </summary>
    [HttpDelete("configs/{id}")]
    public async Task<ActionResult> DeleteConfiguration(int id)
    {
        try
        {
            var config = await _context.GoAnywhereConfigs.FindAsync(id);
            if (config == null)
                return NotFound(new { error = $"Configuration with ID {id} not found" });

            // Get the project to check its path
            var project = await _context.GoAnywhereProjects.FindAsync(config.ProjectId);
            if (project == null)
                return NotFound(new { error = $"Project with ID {config.ProjectId} not found" });

            // Check access control: FCB /production configurations are read-only
            var source = GetConfigSource();
            if (IsProductionConfigProtected(source, project.ProjectPath))
            {
                _logger.LogWarning($"Access denied: Attempted to delete FCB /production config {id} from source {source}");
                return Forbid("FCB /production configurations are read-only and cannot be deleted");
            }

            _context.GoAnywhereConfigs.Remove(config);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Deleted configuration {id}: {config.ConfigKey}");

            return Ok(new { message = $"Configuration {config.ConfigKey} deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting configuration {id}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/goanywhere/summary
    /// Retrieves a summary of GoAnywhere configuration system
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<object>> GetSummary()
    {
        try
        {
            var projectCount = await _context.GoAnywhereProjects.CountAsync();
            var configCount = await _context.GoAnywhereConfigs.CountAsync();
            
            var configsByEnv = await _context.GoAnywhereConfigs
                .GroupBy(c => c.Environment)
                .Select(g => new { Environment = g.Key, Count = g.Count() })
                .ToListAsync();

            var summary = new
            {
                TotalProjects = projectCount,
                TotalConfigurations = configCount,
                ConfigurationsByEnvironment = configsByEnv,
                Status = "Ready"
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving GoAnywhere summary");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// PUT /api/goanywhere/projects/{id}/path
    /// Updates the project path for a GoAnywhere project
    /// </summary>
    [HttpPut("projects/{id}/path")]
    public async Task<ActionResult<GoAnywhereProjectDto>> UpdateProjectPath(
        int id,
        [FromBody] UpdateProjectPathRequest request)
    {
        try
        {
            var project = await _context.GoAnywhereProjects.FindAsync(id);
            
            if (project == null)
                return NotFound(new { error = $"Project with id {id} not found" });

            project.ProjectPath = request.ProjectPath;
            project.LastModifiedDate = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();

            var dto = new GoAnywhereProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                ProjectPath = project.ProjectPath,
                ExtentName = project.ExtentName
            };

            _logger.LogInformation($"Updated ProjectPath for project {id} to '{request.ProjectPath}'");
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating project path for project {id}");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

/// <summary>
/// DTO for GoAnywhere Project
/// </summary>
public class GoAnywhereProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? ProjectPath { get; set; }
    public string? ExtentName { get; set; }
}

/// <summary>
/// DTO for GoAnywhere Configuration
/// </summary>
public class GoAnywhereConfigDto
{
    public int Id { get; set; }
    public string ConfigKey { get; set; } = "";
    public string? ConfigValue { get; set; }
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public bool IsSensitive { get; set; }
}

/// <summary>
/// Request body for updating configuration
/// </summary>
public class UpdateConfigurationRequest
{
    public string? ConfigValue { get; set; }
}

/// <summary>
/// Request body for updating project path
/// </summary>
public class UpdateProjectPathRequest
{
    public string? ProjectPath { get; set; }
}

/// <summary>
/// Request body for creating configuration
/// </summary>
public class CreateConfigurationRequest
{
    public int ProjectId { get; set; }
    public string Environment { get; set; } = "";
    public string ConfigKey { get; set; } = "";
    public string? ConfigValue { get; set; }
    public string? Description { get; set; }
    public bool? IsRequired { get; set; }
    public bool? IsSensitive { get; set; }
}

/// <summary>
/// Request body for creating a project
/// </summary>
public class CreateProjectRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? ProjectPath { get; set; }
    public int? ContextId { get; set; }
    public int? ExtentId { get; set; }
    public string? ExtentName { get; set; }
}
