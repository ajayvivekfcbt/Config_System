using Microsoft.Data.Sqlite;
using ConfigSystem.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Text.RegularExpressions;

namespace ConfigSystem.Api.Data;

public class GoAnywhereSeeder
{
    private readonly ConfigDbContext _context;
    private readonly string _basePath;

    public GoAnywhereSeeder(ConfigDbContext context, string basePath = "")
    {
        _context = context;
        _basePath = string.IsNullOrEmpty(basePath) ? Directory.GetCurrentDirectory() : basePath;
    }

    public async Task SeedAsync()
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS GAPROJECT (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    Description TEXT,
                    ProjectPath TEXT,
                    ContextId INTEGER,
                    ExtentId INTEGER,
                    ExtentName TEXT,
                    CreatedDate TEXT DEFAULT CURRENT_TIMESTAMP,
                    LastModifiedDate TEXT DEFAULT CURRENT_TIMESTAMP
                )
            ");

            await _context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS GACONFIG (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProjectId INTEGER NOT NULL,
                    Environment TEXT NOT NULL,
                    ConfigKey TEXT NOT NULL,
                    ConfigValue TEXT,
                    Description TEXT,
                    IsRequired INTEGER DEFAULT 0,
                    IsSensitive INTEGER DEFAULT 0,
                    CreatedDate TEXT DEFAULT CURRENT_TIMESTAMP,
                    LastModifiedDate TEXT DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(ProjectId) REFERENCES GAPROJECT(Id) ON DELETE CASCADE,
                    UNIQUE(ProjectId, Environment, ConfigKey)
                )
            ");

            var existingProjects = await _context.GoAnywhereProjects.CountAsync();
            var existingConfigs = await _context.GoAnywhereConfigs.CountAsync();

            if (existingProjects > 0 && existingConfigs > 0)
            {
                Console.WriteLine($"GoAnywhere configuration data already exists ({existingProjects} projects, {existingConfigs} configs). Skipping seeding.");
                return;
            }

            Console.WriteLine("Seeding GoAnywhere projects and configurations from CSV...");

            // Load projects from CSV
            var projects = LoadProjectsFromCsv();
            
            if (projects.Count < 10)  // If CSV load returned too few, use fallback
            {
                Console.WriteLine("⚠ CSV load returned insufficient projects, using fallback");
                projects = GetFallbackProjects();
            }

            // Only add new projects if none exist
            if (existingProjects == 0)
            {
                _context.GoAnywhereProjects.AddRange(projects);
                await _context.SaveChangesAsync();
                Console.WriteLine($"✓ Added {projects.Count} GoAnywhere projects from CSV");
            }
            else
            {
                Console.WriteLine($"Projects already exist ({existingProjects}), skipping project creation");
                projects = await _context.GoAnywhereProjects.ToListAsync();
            }

            // Reload projects from database to get correct IDs
            var savedProjects = await _context.GoAnywhereProjects.ToListAsync();
            Console.WriteLine($"Reloaded {savedProjects.Count} projects from database with assigned IDs");

            var configurations = SeedConfigurations(savedProjects.ToArray());
            Console.WriteLine($"Prepared {configurations.Count} configurations for saving...");
            
            // Track duplicates
            var duplicates = configurations
                .GroupBy(c => new { c.ProjectId, c.Environment, c.ConfigKey })
                .Where(g => g.Count() > 1)
                .ToList();
            
            if (duplicates.Any())
            {
                Console.WriteLine($"⚠ WARNING: Found {duplicates.Count} duplicate (ProjectId, Environment, ConfigKey) combinations:");
                foreach (var dup in duplicates.Take(5))
                {
                    var sample = dup.First();
                    Console.WriteLine($"  - Project {sample.ProjectId}, Env {sample.Environment}, Key {sample.ConfigKey}: {dup.Count()} entries");
                }
            }
            
            // Remove duplicates - keep first, remove duplicates
            var uniqueConfigs = configurations
                .GroupBy(c => new { c.ProjectId, c.Environment, c.ConfigKey })
                .Select(g => g.First())
                .ToList();
            
            Console.WriteLine($"After deduplication: {uniqueConfigs.Count} unique configurations");
            
            _context.GoAnywhereConfigs.AddRange(uniqueConfigs);
            await _context.SaveChangesAsync();
            Console.WriteLine($"✓ Added {uniqueConfigs.Count} configuration records");

            // Parse XML files and add configurations
            await ParseAndSeedXmlConfigurations(projects.ToArray());

            // Seed 48 GoAnywhere projects from XML definitions
            await SeedXmlProjectConfigurations();

            PrintSummary(projects.ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during seeding: {ex.Message}");
            throw;
        }
    }

    private List<GoAnywhereProject> LoadProjectsFromCsv()
    {
        var projects = new List<GoAnywhereProject>();
        var seenExtents = new HashSet<int>(); // Track duplicate extent IDs
        
        try
        {
            var csvPath = Path.Combine(_basePath, "Data");
            var xtnFile = Path.Combine(csvPath, "UTCFGXTN.csv");

            if (!File.Exists(xtnFile))
            {
                Console.WriteLine("⚠ UTCFGXTN.csv not found");
                return projects;
            }

            var lines = File.ReadAllLines(xtnFile);
            int projectId = 1;
            int duplicateCount = 0;
            
            // Skip header, parse lines
            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var parts = ParseCsvLine(lines[i]);
                    
                    // Filter for Context 8 (GoAnywhere context)
                    if (parts.Length >= 3 && int.TryParse(parts[1], out int contextId) && contextId == 8)
                    {
                        if (int.TryParse(parts[0], out int extentId))
                        {
                            // Skip duplicate extent IDs
                            if (seenExtents.Contains(extentId))
                            {
                                duplicateCount++;
                                continue;
                            }
                            
                            seenExtents.Add(extentId);
                            var extentName = parts[2].Trim();
                            
                            if (!string.IsNullOrWhiteSpace(extentName))
                            {
                                // Determine ProjectPath based on extent ID
                                // Production projects: 133 (APClearedChecks), 137 (APPositivePay), 146 (FRBServices), 255 (CtlDisbJPMPrev)
                                string projectPath = GetProjectPathForExtent(extentId);
                                
                                projects.Add(new GoAnywhereProject
                                {
                                    Id = projectId++,
                                    Name = extentName,
                                    Description = parts.Length > 3 ? parts[3].Trim() : $"GoAnywhere Project - {extentName}",
                                    ContextId = contextId,
                                    ExtentId = extentId,
                                    ExtentName = extentName,
                                    ProjectPath = projectPath
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠ Error parsing line {i}: {ex.Message}");
                }
            }

            if (duplicateCount > 0)
            {
                Console.WriteLine($"⚠ Skipped {duplicateCount} duplicate extent entries from CSV");
            }
            Console.WriteLine($"✓ Loaded {projects.Count} unique GoAnywhere projects from UTCFGXTN.csv");
            return projects;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Error loading projects from CSV: {ex.Message}");
            return projects;
        }
    }

    private List<GoAnywhereProject> GetFallbackProjects()
    {
        return new[]
        {
            new GoAnywhereProject { Name = "Projects", Description = "GA Project Variables", ExtentId = 3, ExtentName = "Projects", ContextId = 3, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "Config", Description = "Configurations", ExtentId = 28, ExtentName = "Config", ContextId = 3, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "AgriLine", Description = "AgriLine", ExtentId = 54, ExtentName = "AgriLine", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "WellsFargoRDC", Description = "Wells Fargo RDC", ExtentId = 101, ExtentName = "WellsFargoRDC", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "WellsFargoRDCRtn", Description = "Wells Fargo RDC Returns", ExtentId = 102, ExtentName = "WellsFargoRDCRtn", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "MinnMutualGET", Description = "Minnesota Mutual GET", ExtentId = 107, ExtentName = "MinnMutualGET", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "MinnMutualPUT", Description = "Minnesota Mutual PUT", ExtentId = 108, ExtentName = "MinnMutualPUT", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "AgriLineExc", Description = "AgriLine Exceptions", ExtentId = 110, ExtentName = "AgriLineExc", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "PatChecksExport", Description = "Patronage Checks Export", ExtentId = 111, ExtentName = "PatChecksExport", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "PatClearedChecks", Description = "Patronage Cleared Checks", ExtentId = 112, ExtentName = "PatClearedChecks", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "PatPositivePay", Description = "Patronage Positive Pay", ExtentId = 114, ExtentName = "PatPositivePay", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "PatCheckRegister", Description = "Patronage Check Register", ExtentId = 115, ExtentName = "PatCheckRegister", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "PatClearedChecksTest", Description = "Patronage Cleared Checks Test Site", ExtentId = 116, ExtentName = "PatClearedChecksTest", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "PatPositivePayTest", Description = "Patronage Positive Pay Test Site", ExtentId = 117, ExtentName = "PatPositivePayTest", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "LNFHClearedChecksGet", Description = "Loan & Funds Held Cleared Checks Get File from JPM", ExtentId = 120, ExtentName = "LNFHClearedChecksGet", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "LNFHPosPayPayPilot", Description = "Loan & Funds Held Positive Pay from Pay Pilot", ExtentId = 122, ExtentName = "LNFHPosPayPayPilot", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "APClearedChecks", Description = "AP Cleared Checks", ExtentId = 133, ExtentName = "APClearedChecks", ContextId = 8, ProjectPath = "/production" },
            new GoAnywhereProject { Name = "OFAC", Description = "Office of Foreign Assets Control", ExtentId = 135, ExtentName = "OFAC", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "APPositivePay", Description = "AP Positive Pay", ExtentId = 137, ExtentName = "APPositivePay", ContextId = 8, ProjectPath = "/production" },
            new GoAnywhereProject { Name = "APPositivePayAck", Description = "AP Positive Pay Acknowledgement", ExtentId = 136, ExtentName = "APPositivePayAck", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "FRBServices", Description = "Federal Reserve Board Services", ExtentId = 146, ExtentName = "FRBServices", ContextId = 8, ProjectPath = "/production" },
            new GoAnywhereProject { Name = "Payroll", Description = "Payroll File Download from AgFirst", ExtentId = 147, ExtentName = "Payroll", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "PayrollBEN", Description = "Payroll Benefits File Download from AgFirst", ExtentId = 148, ExtentName = "PayrollBEN", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "PayrollUHC", Description = "Payroll UHC Benefit File Download from AgFirst", ExtentId = 149, ExtentName = "PayrollUHC", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "PayrollANN", Description = "Payroll Annual Leave File Download from AgFirst", ExtentId = 150, ExtentName = "PayrollANN", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "CtlDisbJPMPrev", Description = "Controlled Disbursements JPM Chase Previous Day", ExtentId = 255, ExtentName = "CtlDisbJPMPrev", ContextId = 8, ProjectPath = "/production" },
            new GoAnywhereProject { Name = "LockBox", Description = "LockBox Download with Wells Fargo", ExtentId = 182, ExtentName = "LockBox", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "LockBox Processing Wells", Description = "LockBox Process Wells Fargo", ExtentId = 183, ExtentName = "LockBoxPrc", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "EBox", Description = "EBox Processing", ExtentId = 184, ExtentName = "EBox", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "EBox Processing Wells", Description = "EBox Process Wells Fargo", ExtentId = 185, ExtentName = "EBoxPrc", ContextId = 8, ProjectPath = "/betatest" },
            new GoAnywhereProject { Name = "TestAPI", Description = "Test API call with Params", ExtentId = 300, ExtentName = "TestAPI", ContextId = 8, ProjectPath = "/betatest" }
        }.ToList();
    }

    private string GetProjectPathForExtent(int extentId)
    {
        // Production projects based on ExtentId
        var productionExtents = new[] { 133, 137, 146, 255 };
        return productionExtents.Contains(extentId) ? "/production" : "/betatest";
    }

    private List<GoAnywhereConfig> SeedConfigurations(GoAnywhereProject[] projects)
    {
        var configs = new List<GoAnywhereConfig>();
        var projectDict = projects.ToDictionary(p => p.ExtentId); // Use ExtentId as unique key
        var configDict = new Dictionary<(int projectId, string env, string key), GoAnywhereConfig>();

        // Map scopes to environments
        var scopeToEnvironment = new Dictionary<int, string>
        {
            { 1, "FCB" },
            { 10, "DATO" },
            { 11, "DATI" },
            { 12, "DATU" },
            { 13, "DATN" },
            { 19, "DATN" },
            { 20, "DATO" },
            { 21, "DATV" },
            { 22, "DATV" },
            { 23, "DATV" },
            { 27, "DATO" }
        };

        try
        {
            var csvPath = Path.Combine(_basePath, "Data");
            var vdfFile = Path.Combine(csvPath, "UTCFGVDF.csv");
            var valFile = Path.Combine(csvPath, "UTCFGVAL.csv");

            if (!File.Exists(vdfFile) || !File.Exists(valFile))
            {
                Console.WriteLine("⚠ CSV files not found. Using fallback configuration...");
                return GenerateFallbackConfigurations(projects);
            }

            var vdfLines = File.ReadAllLines(vdfFile);
            var valLines = File.ReadAllLines(valFile);

            // Parse VDF CSV (skip header)
            var vdfData = new List<(int vdfId, int extentId, string paramName)>();
            for (int i = 1; i < vdfLines.Length; i++)
            {
                try
                {
                    var parts = ParseCsvLine(vdfLines[i]);
                    if (parts.Length >= 4 && int.TryParse(parts[0], out int vdfId) &&
                        int.TryParse(parts[1], out int extentId))
                    {
                        var paramName = parts[3].Trim();
                        if (!string.IsNullOrWhiteSpace(paramName))
                        {
                            vdfData.Add((vdfId, extentId, paramName));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠ Error parsing VDF line {i}: {ex.Message}");
                }
            }

            // Parse VAL CSV (skip header)
            var valData = new Dictionary<(int vdfId, int scope), string>();
            for (int i = 1; i < valLines.Length; i++)
            {
                try
                {
                    var parts = ParseCsvLine(valLines[i]);
                    if (parts.Length >= 4 && int.TryParse(parts[1], out int vdfId) &&
                        int.TryParse(parts[2], out int scope))
                    {
                        var value = parts[3].Trim();
                        // Use first value found for each scope (avoid overwriting)
                        if (!valData.ContainsKey((vdfId, scope)))
                        {
                            valData[(vdfId, scope)] = value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠ Error parsing VAL line {i}: {ex.Message}");
                }
            }

            Console.WriteLine($"Loaded {vdfData.Count} VDF entries and {valData.Count} VAL entries");

            // For each project, get its parameters from CSV
            foreach (var project in projects)
            {
                if (project.ExtentId == 0) continue;

                var projectVdfs = vdfData.Where(v => v.extentId == project.ExtentId).ToList();

                if (projectVdfs.Count == 0)
                {
                    continue;
                }

                Console.WriteLine($"Project {project.Name} (Extent {project.ExtentId}): {projectVdfs.Count} parameters");

                // Add configurations for each VDF parameter (only if value exists)
                foreach (var (vdfId, _, paramName) in projectVdfs)
                {
                    // Create config entries for all environments
                    foreach (var env in new[] { "DATO", "DATI", "DATU", "DATV", "DATN", "FCB" })
                    {
                        var key = (project.Id, env, paramName);
                        if (!configDict.ContainsKey(key))
                        {
                            // Try to find a value for this parameter in this environment
                            string? paramValue = null;
                            
                            var scopesForEnv = scopeToEnvironment
                                .Where(x => x.Value == env)
                                .Select(x => x.Key)
                                .ToList();

                            foreach (var scope in scopesForEnv)
                            {
                                if (valData.TryGetValue((vdfId, scope), out var value))
                                {
                                    paramValue = value;
                                    break;
                                }
                            }

                            // Only create config if we found a value (don't create empty ones)
                            if (!string.IsNullOrWhiteSpace(paramValue))
                            {
                                var isSensitive = paramName.ToLower().Contains("password") || 
                                                 paramName.ToLower().Contains("secret") ||
                                                 paramName.ToLower().Contains("passphrase") ||
                                                 paramName.ToLower().Contains("key") ||
                                                 paramName.ToLower().Contains("user");

                                configDict[key] = new GoAnywhereConfig
                                {
                                    ProjectId = project.Id,
                                    Environment = env,
                                    ConfigKey = paramName,
                                    ConfigValue = paramValue.Trim(),
                                    Description = $"{paramName} for {project.Name}",
                                    IsRequired = false,
                                    IsSensitive = isSensitive
                                };
                            }
                        }
                    }
                }
            }

            configs = configDict.Values.ToList();
            Console.WriteLine($"✓ Loaded {configs.Count} parameter values from CSV");
            return configs;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Error loading CSV files: {ex.Message}. Stack: {ex.StackTrace}");
            return GenerateFallbackConfigurations(projects);
        }
    }

    private List<GoAnywhereConfig> GenerateFallbackConfigurations(GoAnywhereProject[] projects)
    {
        var configs = new List<GoAnywhereConfig>();
        var environments = new[] { "DATO", "DATI", "DATU", "DATV", "DATN", "FCB" };

        var parameterTemplates = new[]
        {
            ("LocalDirectory", "resource:smb://{project}/{env}/", false),
            ("RemoteDirectory", "resource:ftp://ftp.{env}.example.com/{project}/", false),
            ("ApiEndpoint", "https://api.{env}.example.com/goanywhere", false),
            ("ApiKey", "sk-{env}-{project}-key-12345", true),
            ("ApiSecret", "secret-{env}-{project}-value-67890", true),
            ("RetryAttempts", "3", false),
            ("RetryDelaySeconds", "60", false),
            ("Timeout", "300", false),
            ("LogLevel", "INFO", false),
            ("EnableNotifications", "true", false),
            ("NotificationEmail", "admin@{env}.example.com", false),
            ("MaxConcurrentConnections", "5", false),
            ("ProxyEnabled", "false", false),
            ("ProxyHost", "proxy.{env}.example.com", false),
            ("ProxyPort", "8080", false),
            ("DatabaseHost", "db-{env}.example.com", false),
            ("DatabasePort", "5432", false),
            ("DatabaseName", "{project}_{env}", false),
            ("DatabaseUser", "gauser_{env}", false),
            ("DatabasePassword", "dbpass_{env}_{project}", true),
            ("SSLEnabled", "true", false),
            ("SSLCertPath", "/etc/ssl/certs/{env}/{project}.pem", false),
            ("ArchiveEnabled", "true", false),
            ("ArchivePath", "s3://archive-{env}/{project}/", false),
            ("ErrorHandling", "retry_then_alert", false),
            ("MessageFormat", "JSON", false),
        };

        foreach (var project in projects)
        {
            // Special handling for TestAPI project
            if (project.Name == "TestAPI")
            {
                foreach (var env in environments)
                {
                    var testApiParams = new[]
                    {
                        ("MailServer", "MailServer", false),
                        ("toList", "ajayvivek.pillai@farmcreditbank.com", false),
                        ("signingAlgorithm", "MD5", false),
                        ("keyLocation", "KeyVault", false),
                        ("from", "ajayvivek.pillai@farmcreditbank.com", false),
                        ("subject", "Test", false),
                        ("message", "Value passed as param - ${Text}", false),
                        ("Text", "Dummy", false),
                        ("version", "2.0", false),
                        ("logLevel", "verbose", false)
                    };

                    foreach (var (paramName, value, isSensitive) in testApiParams)
                    {
                        configs.Add(new GoAnywhereConfig
                        {
                            ProjectId = project.Id,
                            Environment = env,
                            ConfigKey = paramName,
                            ConfigValue = value,
                            Description = $"{paramName} configuration for {project.Name}",
                            IsRequired = paramName == "MailServer" || paramName == "toList" || paramName == "from",
                            IsSensitive = isSensitive
                        });
                    }
                }
            }
            else
            {
                // Default fallback parameters for other projects
                foreach (var env in environments)
                {
                    foreach (var (paramName, template, isSensitive) in parameterTemplates)
                    {
                        var value = template
                            .Replace("{project}", project.Name.ToLower())
                            .Replace("{env}", env.ToLower());

                        configs.Add(new GoAnywhereConfig
                        {
                            ProjectId = project.Id,
                            Environment = env,
                            ConfigKey = paramName,
                            ConfigValue = value,
                            Description = $"{paramName} configuration for {project.Name}",
                            IsRequired = paramName.Contains("Endpoint") || paramName.Contains("Key") || paramName.Contains("Host"),
                            IsSensitive = isSensitive
                        });
                    }
                }
            }
        }

        Console.WriteLine($"✓ Generated {configs.Count} fallback configurations for {projects.Length} projects");
        return configs;
    }

    private string[] ParseCsvLine(string line)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                parts.Add(current.ToString().Trim('"').Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString().Trim('"').Trim());
        }

        return parts.ToArray();
    }

    private async Task ParseAndSeedXmlConfigurations(GoAnywhereProject[] projects)
    {
        Console.WriteLine("\n📄 Parsing XML files for additional configurations...");
        Console.WriteLine($"   Projects available for matching: {projects.Length}");
        
        try
        {
            // GoAnywhere folder is one level up from the application folder
            var backendFolder = Path.GetDirectoryName(_basePath);
            if (string.IsNullOrEmpty(backendFolder))
            {
                Console.WriteLine("⚠ Could not determine backend folder path");
                return;
            }
            
            var xmlFolder = Path.Combine(backendFolder, "GoAnywhere");
            
            if (!Directory.Exists(xmlFolder))
            {
                Console.WriteLine($"⚠ GoAnywhere folder not found at: {xmlFolder}");
                return;
            }

            var xmlFiles = Directory.GetFiles(xmlFolder, "*.xml");
            if (xmlFiles.Length == 0)
            {
                Console.WriteLine("⚠ No XML files found in GoAnywhere folder");
                return;
            }

            Console.WriteLine($"   Found {xmlFiles.Length} XML files to process");

            int xmlConfigsAdded = 0;
            var environments = new[] { "DATO", "DATI", "DATU", "DATV", "DATN", "FCB" };

            foreach (var xmlFile in xmlFiles)
            {
                try
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.Load(xmlFile);

                    // Get project name from XML
                    var projectNode = xmlDoc.DocumentElement;
                    var projectName = projectNode?.GetAttribute("name");

                    if (string.IsNullOrWhiteSpace(projectName))
                    {
                        Console.WriteLine($"   ⚠ No project name found in {Path.GetFileName(xmlFile)}");
                        continue;
                    }

                    Console.WriteLine($"   Processing: {projectName}");

                    // Find matching project in database
                    var project = projects.FirstOrDefault(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
                    if (project == null)
                    {
                        // Try exact match first
                        project = projects.FirstOrDefault(p => p.Name == projectName);
                    }

                    if (project == null)
                    {
                        Console.WriteLine($"     ✗ Project '{projectName}' not found in database");
                        continue;
                    }

                    Console.WriteLine($"     ✓ Matched to ProjectId: {project.Id}");

                    var xmlConfigs = new List<GoAnywhereConfig>();

                    // Extract resource IDs
                    var resourceNodes = xmlDoc.SelectNodes("//@resourceId");
                    var uniqueResources = new HashSet<string>();
                    int resourceCount = 0;

                    foreach (XmlAttribute attr in resourceNodes)
                    {
                        var resourceId = attr.Value?.Trim();
                        if (!string.IsNullOrEmpty(resourceId) && uniqueResources.Add(resourceId))
                        {
                            foreach (var env in environments)
                            {
                                xmlConfigs.Add(new GoAnywhereConfig
                                {
                                    ProjectId = project.Id,
                                    Environment = env,
                                    ConfigKey = $"ResourceId_{resourceId}",
                                    ConfigValue = resourceId,
                                    Description = $"SFTP/Connection Resource from XML: {resourceId}",
                                    IsSensitive = false,
                                    IsRequired = false
                                });
                            }
                            resourceCount++;
                        }
                    }

                    if (resourceCount > 0)
                    {
                        Console.WriteLine($"     Found {resourceCount} unique resources");
                    }

                    // Extract variables from XML (${VariableName} pattern)
                    var xmlText = File.ReadAllText(xmlFile);
                    var pattern = @"\$\{(\w+)\}";
                    var matches = Regex.Matches(xmlText, pattern);
                    var uniqueVars = new HashSet<string>();
                    int varCount = 0;

                    foreach (Match match in matches)
                    {
                        var varName = match.Groups[1].Value;
                        if (!string.IsNullOrEmpty(varName) && uniqueVars.Add(varName))
                        {
                            var isSensitive = varName.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                                            varName.Contains("key", StringComparison.OrdinalIgnoreCase) ||
                                            varName.Contains("secret", StringComparison.OrdinalIgnoreCase);

                            foreach (var env in environments)
                            {
                                xmlConfigs.Add(new GoAnywhereConfig
                                {
                                    ProjectId = project.Id,
                                    Environment = env,
                                    ConfigKey = varName,
                                    ConfigValue = "",
                                    Description = $"Variable from XML: {varName}",
                                    IsSensitive = isSensitive,
                                    IsRequired = false
                                });
                            }
                            varCount++;
                        }
                    }

                    if (varCount > 0)
                    {
                        Console.WriteLine($"     Found {varCount} unique variables");
                    }

                    // Add unique configurations
                    var newConfigs = xmlConfigs
                        .GroupBy(c => new { c.ProjectId, c.Environment, c.ConfigKey })
                        .Select(g => g.First())
                        .Where(c => !_context.GoAnywhereConfigs.Any(existing =>
                            existing.ProjectId == c.ProjectId &&
                            existing.Environment == c.Environment &&
                            existing.ConfigKey == c.ConfigKey))
                        .ToList();

                    if (newConfigs.Count > 0)
                    {
                        _context.GoAnywhereConfigs.AddRange(newConfigs);
                        await _context.SaveChangesAsync();
                        xmlConfigsAdded += newConfigs.Count;
                        Console.WriteLine($"     ✓ Added {newConfigs.Count} configurations");
                    }
                    else
                    {
                        Console.WriteLine($"     → No new configurations to add (already exist)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ✗ Error processing {Path.GetFileName(xmlFile)}: {ex.Message}");
                }
            }

            if (xmlConfigsAdded > 0)
            {
                Console.WriteLine($"✓ Total XML configurations added: {xmlConfigsAdded}");
            }
            else
            {
                Console.WriteLine($"→ No new XML configurations added");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error parsing XML files: {ex.Message}");
        }
    }

    private void PrintSummary(GoAnywhereProject[] projects)
    {
        Console.WriteLine("\n========== GoAnywhere Configuration Summary ==========");
        Console.WriteLine($"Total Projects: {projects.Length}");
        Console.WriteLine($"Total Configurations: {_context.GoAnywhereConfigs.Count()}");

        var byEnvironment = _context.GoAnywhereConfigs
            .GroupBy(c => c.Environment)
            .Select(g => new { Environment = g.Key, Count = g.Count() })
            .OrderBy(x => x.Environment)
            .ToList();

        Console.WriteLine("\nConfigurations by Environment:");
        foreach (var item in byEnvironment)
        {
            Console.WriteLine($"  {item.Environment}: {item.Count} records");
        }
        Console.WriteLine("======================================================\n");
    }

    public async Task SeedXmlProjectConfigurations()
    {
        Console.WriteLine("\n📋 Seeding GoAnywhere Projects from XML project definitions...");
        
        var xmlProjectsData = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<projects>
<project name=""AGSWEEP COPY"" description=""Copy the Daily Combined Nacha file from the IFS staging folder into the Cash Operations CombinedACHDailyFiles folder"">
  <variable name=""Resource"" value=""AgSweepBAI Dev"" />
  <variable name=""RemoteFile"" value="""" />
  <variable name=""StagingDir"" value="""" />
  <variable name=""StagingFile"" value="""" />
  <variable name=""HostServer"" value="""" />
  <variable name=""HostLibrary"" value="""" />
  <variable name=""Collection"" value="""" />
  <variable name=""EndStatus"" value="""" />
</project>
<project name=""Copy4100Files"" description=""Copies select pdf's from the IFS to a 110 Shared Network folder"">
  <variable name=""ProcessDate"" value="""" />
  <variable name=""Err4100"" value=""False"" />
  <variable name=""ErrB41N"" value=""False"" />
  <variable name=""ErrB411"" value=""False"" />
  <variable name=""Err4101"" value=""False"" />
</project>
<project name=""Copy4128"" description=""Copy the LA4128 report to network share"">
  <variable name=""Resource"" value=""LA4128"" />
  <variable name=""Month"" value="""" />
  <variable name=""Year"" value="""" />
</project>
<project name=""CopyFolderData"" description=""Clears a DestinationDirectory, then copys all contents of a FromDirectory to it"">
  <variable name=""FromDir"" value=""/Distribution/110/CardinalReportsDaily"" />
  <variable name=""DestDir"" value=""/110/CardinalReportsDaily"" />
  <variable name=""Resource"" value=""PDFData"" />
  <variable name=""CopyFirst"" value=""0"" />
  <variable name=""CopyDir"" value=""/110/CardinalReportsDailyOld"" />
  <variable name=""ClearError"" value=""0"" />
</project>
<project name=""CopyFolderDataEOY"" description=""Copys the wildcard files in a From Directory to Destination Directory at Year End"">
  <variable name=""FromDir"" value="""" />
  <variable name=""DestDir"" value="""" />
  <variable name=""Resource"" value="""" />
  <variable name=""ProcessDate"" value="""" />
  <variable name=""ClearError"" value=""9"" />
</project>
<project name=""CopyIFSFile"" description=""Copy a file From/To the IFS From/To a Shared Network folder"">
  <variable name=""Action"" value=""PULL"" />
  <variable name=""IFSFile"" value=""/DATCOMN/AP_ClearedChecks/oh00511a.txt"" />
  <variable name=""NetworkFile"" value=""/110/AccountsPayable/APClearedChecks.txt"" />
  <variable name=""Resource"" value=""PDFData"" />
</project>
<project name=""CopyIFSFileScreen"" description=""Copy a file From/To the IFS From/To a Shared Network folder"">
  <variable name=""Resource"" value=""PDFData"" />
  <variable name=""IFSFile"" value=""/U4142AP/testnew/"" />
  <variable name=""DestDir"" value=""/Test/CHG0036987/"" />
  <variable name=""ErrorFlg"" value=""0"" />
</project>
<project name=""CopyMASK"" description=""Copy iSeries MASK table data to a CSV file"">
  <variable name=""ExportModule"" value="""" />
  <variable name=""ExportNumber"" value="""" />
  <variable name=""ExportVersion"" value="""" />
  <variable name=""ExportFormat"" value="""" />
  <variable name=""Document"" value="""" />
  <variable name=""DBServer"" value="""" />
  <variable name=""Staging"" value="""" />
  <variable name=""Resource"" value="""" />
</project>
<project name=""CopyNachaFile"" description=""Copy the Daily Combined Nacha file from the IFS staging folder into the Cash Operations CombinedACHDailyFiles folder"">
  <variable name=""Share"" value=""Nacha"" />
  <variable name=""StagingDir"" value="""" />
  <variable name=""StagingFile"" value="""" />
  <variable name=""Cutoff"" value="""" />
</project>
<project name=""CopyPayrollReports"" description=""Copy a Payroll Reports From the IFS To a Shared Network folder"">
  <variable name=""ArchiveDir"" value="""" />
  <variable name=""ZipFileSet"" value="""" />
  <variable name=""ZipFile"" value="""" />
  <variable name=""IFSFile"" value="""" />
  <variable name=""Resource"" value="""" />
</project>
<project name=""CRHFMUpload"" description=""User Adjustments upload to Corp Rptng"">
  <variable name=""Resource"" value=""MASKCsv"" />
  <variable name=""Archive"" value=""MASKCsv"" />
  <variable name=""OutputFile"" value=""DATCOMN.CRHFMRAW"" />
  <variable name=""CSVfile"" value=""/CRHFMUpload.csv"" />
  <variable name=""User"" value=""UCSQLFIN"" />
  <variable name=""Password"" value=""bendovr1"" />
  <variable name=""Host"" value=""FCB"" />
</project>
<project name=""CRUpload"" description=""User Adjustments upload to Corp Rptng"">
  <variable name=""Resource"" value=""MASKCsv"" />
  <variable name=""Archive"" value=""MASKCsv"" />
  <variable name=""OutputFile"" value=""DATCOMN.CRUADJRAW"" />
  <variable name=""CSVfile"" value=""/CRUploadQtrly.csv"" />
  <variable name=""User"" value=""UCSQLFIN"" />
  <variable name=""Password"" value=""bendovr1"" />
  <variable name=""Host"" value=""FCB"" />
</project>
<project name=""DistributionSave"" description=""Copy the /Distribution folder contents"" />
<project name=""EBox Processing Wells"" description=""Copies Todays EBox file into iSeries table"">
  <variable name=""LocalDirectory"" value=""resource:smb://Ebox/iSeries"" />
  <variable name=""iSeriesFile"" value=""DATCOMN.CEP1652"" />
  <variable name=""iSeriesServer"" value=""FCB"" />
</project>
<project name=""FRB Services ABA Download"" description=""copies data into DATCOMN/FCABAV"">
  <variable name=""Library"" value=""DATCOMN"" />
  <variable name=""Resource"" value=""resource:share://FEDABA/"" />
</project>
<project name=""FTP Get doc sub-project Local"" description=""Get a document from a server using FTP"" />
<project name=""FTP Get doc sub-project"" description=""Gets a document from a server using FTP"" />
<project name=""FTP Put doc sub-project Local"" description=""Put a document from an iSeries table"" />
<project name=""FTP Put doc sub-project SSNACHA"" description=""Put the SSNACHA document"" />
<project name=""FTP Put doc sub-project"" description=""Put a document from an iSeries table"" />
<project name=""IFS_Export_txt_Folder"" description=""Copys txt file passed as IFS location"">
  <variable name=""IFS_loc"" value=""/home/U4142AP/LNDATA_PNT_01_Jul_2025.csv"" />
  <variable name=""ParentId"" value=""99"" />
  <variable name=""MonthYr"" value=""Sept_2025"" />
  <variable name=""RemoteLoc2Copy"" value=""PwcData"" />
  <variable name=""Email"" value=""DLBatchnotification-BARLO"" />
</project>
<project name=""InfoImage Send Files"" description=""Sends files to InfoImage using SFTP with PGP encryption"">
  <variable name=""EncryptKeyId"" value=""0x82C696DCE176ED64"" />
  <variable name=""LocalShare"" value=""InfoImageProd"" />
  <variable name=""RemoteDirectory"" value=""/test/billing"" />
  <variable name=""Resource"" value=""InfoImage"" />
  <variable name=""Wildcard"" value=""*simpbill_*"" />
</project>
<project name=""LockBox Processing Wells"" description=""Copies Todays LockBox file into iSeries table"">
  <variable name=""LocalDirectory"" value=""resource:smb://LockBox/"" />
  <variable name=""iSeriesFile"" value=""DATLCOMN.CEP1652"" />
  <variable name=""iSeriesServer"" value=""DEV"" />
</project>
<project name=""MarKtAccessAlertExt"" description=""Generates and Emails a CSV file"">
  <variable name=""IFSFolder"" value=""/MarketAccess/"" />
  <variable name=""Library"" value=""DATCOMN"" />
  <variable name=""eMail"" value=""DLFinancialReporting"" />
</project>
<project name=""Master Document Transport Local"" description=""Standard Master MFT Transport functionality"" />
<project name=""Master Document Transport"" description=""Standard Master MFT Transport functionality"">
  <variable name=""Collection"" value=""4162"" />
  <variable name=""Document"" value=""4158"" />
  <variable name=""HostLibrary"" value=""OSBETADATD"" />
  <variable name=""HostServer"" value=""DEV"" />
  <variable name=""MFTMethod"" value=""*COPY"" />
  <variable name=""Resource"" value=""Nacha Dev"" />
</project>
<project name=""mRDCgetCardinal"" description=""mRDC Staging - Load MRDCDATA from CashMgmt database"">
  <variable name=""Association"" value=""CAFC"" />
  <variable name=""CorpCode"" value=""T30"" />
  <variable name=""DBResourceI"" value=""DEV"" />
  <variable name=""DBResourceWin"" value=""mRDC"" />
  <variable name=""StageLibrary"" value=""OSBETADATM"" />
</project>
<project name=""mRDCNachaGet"" description=""GET the daily Nacha transaction file from Ensenta"">
  <variable name=""NetworkResource"" value=""resource:share://Mobile RDC/"" />
  <variable name=""NetworkFile"" value=""AgTexas"" />
  <variable name=""SFTPFile"" value=""AGTXAgTexas*"" />
  <variable name=""SFTPResource"" value=""Ensenta"" />
</project>
<project name=""NACHA COPY"" description=""Copy the Daily Combined Nacha file"">
  <variable name=""Resource"" value=""ACH Dev"" />
</project>
<project name=""new"" description="""" />
<project name=""NWMONYUPD"" description=""Temporary Project to update the new money amt"">
  <variable name=""HostServer"" value=""FCB"" />
  <variable name=""Resource"" value=""CME"" />
  <variable name=""LibraryName"" value=""DATCOMN"" />
</project>
<project name=""OFAC Summary Rpt"" description=""Build an email summary of the OFAC data"">
  <variable name=""Server"" value=""DEV"" />
  <variable name=""Library"" value=""U4142LN"" />
</project>
<project name=""OFACSend"" description=""Create and send batch files for OFAC"" />
<project name=""P2Go Generic file"" description=""Download the daily Payments 2 Go Generic file"">
  <variable name=""ToEmail"" value=""larry.neal2@farmcreditbank.com"" />
  <variable name=""BaseDirectory"" value=""OUT_EOD_Extract"" />
</project>
<project name=""P2Go Posting file"" description=""Download the daily Payments 2 Go Posting file"">
  <variable name=""ToEmail"" value=""larry.neal2@farmcreditbank.com"" />
  <variable name=""BaseDirectory"" value=""OUT_EOD_Transformer"" />
</project>
<project name=""P2Gprep"" description=""Adhoc process to clean up files at Finastra"">
  <variable name=""Resource"" value=""P2G UAT"" />
</project>
<project name=""Patronage Checks Cashed"" description=""Generates and Emails a CSV file"">
  <variable name=""IFSFolder"" value=""/DATCOMN/Patronage/Reports/"" />
  <variable name=""Email"" value=""DLALM@farmcreditbank.com"" />
</project>
<project name=""Payroll"" description=""Downloads one of the Payroll files from AgFirst"">
  <variable name=""RemoteFile"" value=""prglee.txt"" />
  <variable name=""LocalFile"" value=""/DATICOMN/Payroll/prglee.txt"" />
  <variable name=""ArchiveDir"" value=""/DATCOMN/Payroll/Archive/"" />
</project>
<project name=""Q2 Paid Loans LoanIQ"" description=""Send Q2 our Daily Paid Loans Files"">
  <variable name=""RollLibrary"" value=""DATROLL"" />
  <variable name=""NetworkResource"" value=""Q2PaidLoans"" />
  <variable name=""SFTPResource"" value=""Q2SFTP"" />
</project>
<project name=""SendEmail"" description=""Send Email - from, to, subject"">
  <variable name=""SMTPServer"" value=""binky.nterprise.net"" />
  <variable name=""FromAddress"" value=""DEV@farmcreditbank.com"" />
  <variable name=""ToAddress"" value=""Geoffrey.Neale@FarmCreditBank.com"" />
</project>
<project name=""SFTP PGP Put InfoImage Checks"" description=""Using SFTP connect to InfoImage Check Printing server"" />
<project name=""SFTP PGP Put InfoImage Zip Files"" description=""Send 1 file or a group of files zipped"" />
<project name=""SFTP Process with PGP"" description=""This is an SFTP process that will either GET or PUT PGP files"" />
<project name=""SFTP Process"" description=""This is a generic SFTP process"">
  <variable name=""Resource"" value=""WellsFargo"" />
  <variable name=""Direction"" value=""GET"" />
</project>
<project name=""SFTPGetFiles"" description=""Retrieve file(s) using SFTP"">
  <variable name=""ArchiveFolder"" value=""/Archive"" />
  <variable name=""LocalFile"" value=""JPM_Previous_Day.txt"" />
  <variable name=""Resource"" value=""Chase"" />
</project>
<project name=""Tax - Create JWK"" description=""Create and send our Tax Certificate to the IRS"" />
<project name=""Test"" description="""">
  <variable name=""Parm1"" value=""Parm One"" />
</project>
<project name=""TPG"" description="""">
  <variable name=""Resource"" value=""TPG_DEV"" />
  <variable name=""Archive"" value=""TPG_DevArchive"" />
  <variable name=""OutputFile"" value=""DATCOMN.CRTPGMVRAW"" />
  <variable name=""CSVfile"" value=""/QtrlyRptgExtract.csv"" />
  <variable name=""User"" value=""UCSQLFIN"" />
  <variable name=""Password"" value=""bendovr1"" />
  <variable name=""Host"" value=""FCB"" />
</project>
<project name=""TestAPI"" description=""Test API call with Params"">
  <variable name=""Text"" value=""Dummy"" />
</project>
</projects>";

        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlProjectsData);
            
            var environments = new[] { "DATO", "DATI", "DATU", "DATV", "DATN", "FCB" };
            var projectsProcessed = 0;
            var configsAdded = 0;

            var projectNodes = xmlDoc.SelectNodes("//project");
            
            foreach (XmlElement projectNode in projectNodes)
            {
                var projectName = projectNode.GetAttribute("name");
                var projectDesc = projectNode.GetAttribute("description");
                
                if (string.IsNullOrEmpty(projectName))
                    continue;

                projectsProcessed++;
                
                // Check if project already exists
                var existingProject = await _context.GoAnywhereProjects
                    .FirstOrDefaultAsync(p => p.Name == projectName);
                
                if (existingProject == null)
                {
                    // Create new project
                    existingProject = new GoAnywhereProject
                    {
                        Name = projectName,
                        Description = projectDesc,
                        ProjectPath = "/DEV/Ajay",  // Default project path for XML-sourced projects
                        ExtentId = 999 + projectsProcessed, // Use temp ID
                        ExtentName = projectName,
                        ContextId = 8
                    };
                    _context.GoAnywhereProjects.Add(existingProject);
                    await _context.SaveChangesAsync();
                }

                // Add variables as configurations
                var variableNodes = projectNode.SelectNodes("variable");
                foreach (XmlElement variable in variableNodes)
                {
                    var varName = variable.GetAttribute("name");
                    var varValue = variable.GetAttribute("value");
                    
                    if (string.IsNullOrEmpty(varName))
                        continue;

                    var isSensitive = varName.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                                    varName.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                                    varName.Contains("key", StringComparison.OrdinalIgnoreCase) ||
                                    varName.Contains("passphrase", StringComparison.OrdinalIgnoreCase);

                    foreach (var env in environments)
                    {
                        var existingConfig = await _context.GoAnywhereConfigs
                            .FirstOrDefaultAsync(c => 
                                c.ProjectId == existingProject.Id &&
                                c.Environment == env &&
                                c.ConfigKey == varName);
                        
                        if (existingConfig == null)
                        {
                            var config = new GoAnywhereConfig
                            {
                                ProjectId = existingProject.Id,
                                Environment = env,
                                ConfigKey = varName,
                                ConfigValue = varValue ?? "",
                                Description = $"Variable from XML project definition",
                                IsRequired = false,
                                IsSensitive = isSensitive
                            };
                            _context.GoAnywhereConfigs.Add(config);
                            configsAdded++;
                        }
                    }
                }
            }

            if (configsAdded > 0)
            {
                await _context.SaveChangesAsync();
                Console.WriteLine($"\n✓ Imported {projectsProcessed} projects with {configsAdded} configurations from XML");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error seeding XML projects: {ex.Message}");
        }
    }
}
