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
    /// GET /api/goanywhere/diagnostics
    /// Retrieves diagnostic information about configuration population
    /// </summary>
    [HttpGet("diagnostics")]
    public async Task<ActionResult<object>> GetDiagnostics()
    {
        try
        {
            var projects = await _context.GoAnywhereProjects.ToListAsync();
            var projectsWithoutConfigs = new List<object>();

            foreach (var project in projects)
            {
                var configCount = await _context.GoAnywhereConfigs
                    .Where(c => c.ProjectId == project.Id)
                    .CountAsync();

                if (configCount == 0)
                {
                    projectsWithoutConfigs.Add(new
                    {
                        ProjectId = project.Id,
                        ProjectName = project.Name,
                        Description = project.Description
                    });
                }
            }

            var environments = await _context.GoAnywhereConfigs
                .Select(c => c.Environment)
                .Distinct()
                .OrderBy(e => e)
                .ToListAsync();

            // Load configs with projects, then group in memory (SQLite doesn't support SQL APPLY)
            var configsWithProjects = await _context.GoAnywhereConfigs
                .Include(c => c.Project)
                .ToListAsync();

            var configsByProject = configsWithProjects
                .GroupBy(c => c.Project.Name)
                .Select(g => new
                {
                    ProjectName = g.Key,
                    ConfigCount = g.Count(),
                    Environments = g.Select(c => c.Environment).Distinct().OrderBy(e => e).ToList()
                })
                .OrderByDescending(x => x.ConfigCount)
                .Take(10)
                .ToList();

            var diagnostics = new
            {
                TotalProjects = projects.Count,
                ProjectsWithConfigurations = projects.Count - projectsWithoutConfigs.Count,
                ProjectsWithoutConfigurations = projectsWithoutConfigs.Count,
                ProjectsWithoutConfigsList = projectsWithoutConfigs.OrderBy(p => ((dynamic)p).ProjectName),
                TotalConfigurations = await _context.GoAnywhereConfigs.CountAsync(),
                AvailableEnvironments = environments,
                ConfigsByProject = configsByProject
            };

            return Ok(diagnostics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving GoAnywhere diagnostics");
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

    /// <summary>
    /// GET /api/goanywhere/parameters
    /// Gets all unique parameter keys with their usage count by environment
    /// </summary>
    [HttpGet("parameters")]
    public async Task<ActionResult<IEnumerable<object>>> GetParameters([FromQuery] string? environment = null)
    {
        try
        {
            var query = _context.GoAnywhereConfigs.AsQueryable();
            
            if (!string.IsNullOrWhiteSpace(environment))
            {
                query = query.Where(c => c.Environment == environment);
            }

            var parameters = await query
                .GroupBy(c => new { c.ConfigKey, c.Environment })
                .Select(g => new
                {
                    ConfigKey = g.Key.ConfigKey,
                    Environment = g.Key.Environment,
                    ProjectCount = g.Select(c => c.ProjectId).Distinct().Count(),
                    SampleValue = g.FirstOrDefault().ConfigValue,
                    IsSensitive = g.FirstOrDefault().IsSensitive
                })
                .OrderBy(p => p.Environment)
                .ThenBy(p => p.ConfigKey)
                .ToListAsync();

            _logger.LogInformation($"Retrieved {parameters.Count} unique parameter keys");
            return Ok(parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving parameters");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/goanywhere/parameters/{configKey}/{environment}
    /// Gets all configurations for a specific parameter key and environment across all projects
    /// </summary>
    [HttpGet("parameters/{configKey}/{environment}")]
    public async Task<ActionResult<object>> GetParameterDetails(string configKey, string environment)
    {
        try
        {
            var configs = await _context.GoAnywhereConfigs
                .Where(c => c.ConfigKey == configKey && c.Environment == environment)
                .Include(c => c.Project)
                .OrderBy(c => c.Project.Name)
                .ToListAsync();

            if (!configs.Any())
                return NotFound(new { error = $"No configurations found for key '{configKey}' in environment '{environment}'" });

            var firstConfig = configs.First();
            var result = new
            {
                ConfigKey = configKey,
                Environment = environment,
                IsSensitive = firstConfig.IsSensitive,
                IsRequired = firstConfig.IsRequired,
                Description = firstConfig.Description,
                ProjectCount = configs.Count,
                Configurations = configs.Select(c => new
                {
                    Id = c.Id,
                    ProjectId = c.ProjectId,
                    ProjectName = c.Project.Name,
                    ProjectPath = c.Project.ProjectPath,
                    ConfigValue = c.IsSensitive ? "●●●●●●●●" : c.ConfigValue,
                    ActualValue = c.ConfigValue,
                    IsSensitive = c.IsSensitive
                }).ToList()
            };

            _logger.LogInformation($"Retrieved {configs.Count} configurations for parameter '{configKey}' in '{environment}'");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving parameter details for {configKey}/{environment}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// PUT /api/goanywhere/parameters/{configKey}/{environment}
    /// Updates the same parameter value across all projects for a specific environment
    /// </summary>
    [HttpPut("parameters/{configKey}/{environment}")]
    public async Task<ActionResult<object>> UpdateParameterAcrossProjects(
        string configKey,
        string environment,
        [FromBody] UpdateParameterRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ConfigValue))
                return BadRequest(new { error = "ConfigValue is required" });

            var configs = await _context.GoAnywhereConfigs
                .Where(c => c.ConfigKey == configKey && c.Environment == environment)
                .ToListAsync();

            if (!configs.Any())
                return NotFound(new { error = $"No configurations found for key '{configKey}' in environment '{environment}'" });

            int updateCount = 0;
            foreach (var config in configs)
            {
                config.ConfigValue = request.ConfigValue;
                config.LastModifiedDate = DateTime.UtcNow;
                updateCount++;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Updated {updateCount} configurations for parameter '{configKey}' in environment '{environment}'");

            return Ok(new
            {
                Message = $"Updated {updateCount} project(s)",
                ConfigKey = configKey,
                Environment = environment,
                NewValue = request.ConfigValue,
                AffectedProjects = updateCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating parameter {configKey}/{environment}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// PUT /api/goanywhere/parameters/{configKey}/bulk
    /// Updates the same parameter value across multiple environments for all projects
    /// </summary>
    [HttpPut("parameters/{configKey}/bulk")]
    public async Task<ActionResult<object>> UpdateParameterAcrossEnvironments(
        string configKey,
        [FromBody] UpdateParameterBulkRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ConfigValue))
                return BadRequest(new { error = "ConfigValue is required" });

            if (!request.Environments?.Any() ?? true)
                return BadRequest(new { error = "At least one environment must be selected" });

            var configs = await _context.GoAnywhereConfigs
                .Where(c => c.ConfigKey == configKey && request.Environments.Contains(c.Environment))
                .ToListAsync();

            if (!configs.Any())
                return NotFound(new { error = $"No configurations found for key '{configKey}' in selected environments" });

            int updateCount = 0;
            var envUpdated = new Dictionary<string, int>();

            foreach (var config in configs)
            {
                config.ConfigValue = request.ConfigValue;
                config.LastModifiedDate = DateTime.UtcNow;
                updateCount++;

                if (!envUpdated.ContainsKey(config.Environment))
                    envUpdated[config.Environment] = 0;
                envUpdated[config.Environment]++;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Updated {updateCount} configurations for parameter '{configKey}' across {request.Environments.Count} environments");

            return Ok(new
            {
                Message = $"Updated {updateCount} configuration(s) across {request.Environments.Count} environment(s)",
                ConfigKey = configKey,
                Environments = request.Environments,
                NewValue = request.ConfigValue,
                TotalAffected = updateCount,
                ByEnvironment = envUpdated
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating parameter {configKey} across environments");
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

/// <summary>
/// Request body for updating a parameter value across all projects
/// </summary>
public class UpdateParameterRequest
{
    public string? ConfigValue { get; set; }
}

/// <summary>
/// Request body for updating a parameter value across multiple environments
/// </summary>
public class UpdateParameterBulkRequest
{
    public string? ConfigValue { get; set; }
    public List<string>? Environments { get; set; }
}
