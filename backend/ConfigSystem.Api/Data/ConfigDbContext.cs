using ConfigSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ConfigSystem.Api.Data;

public class ConfigDbContext : DbContext
{
    public ConfigDbContext(DbContextOptions<ConfigDbContext> options) : base(options) { }

    public DbSet<VariableType> VariableTypes => Set<VariableType>();
    public DbSet<ScopeResolutionMethod> ScopeResolutionMethods => Set<ScopeResolutionMethod>();
    public DbSet<Server> Servers => Set<Server>();
    public DbSet<Context> Contexts => Set<Context>();
    public DbSet<Extent> Extents => Set<Extent>();
    public DbSet<Scope> Scopes => Set<Scope>();
    public DbSet<VariableDefinition> VariableDefinitions => Set<VariableDefinition>();
    public DbSet<ValidValue> ValidValues => Set<ValidValue>();
    public DbSet<VariableValue> VariableValues => Set<VariableValue>();
    
    // GoAnywhere Configuration Management
    public DbSet<GoAnywhereProject> GoAnywhereProjects => Set<GoAnywhereProject>();
    public DbSet<GoAnywhereConfig> GoAnywhereConfigs => Set<GoAnywhereConfig>();
    public DbSet<GoAnywhereAuditLog> GoAnywhereAuditLogs => Set<GoAnywhereAuditLog>();
    
    // IBM i Configuration System Audit Logging
    public DbSet<IBMiAuditLog> IBMiAuditLogs => Set<IBMiAuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<VariableType>().ToTable("UTCFGVTP");
        b.Entity<ScopeResolutionMethod>().ToTable("UTCFGSRM");
        b.Entity<Server>().ToTable("UTCFGSRV");
        b.Entity<Context>().ToTable("UTCFGCTX");
        b.Entity<Extent>().ToTable("UTCFGXTN");
        b.Entity<Scope>().ToTable("UTCFGSCP");
        b.Entity<VariableDefinition>().ToTable("UTCFGVDF");
        b.Entity<ValidValue>().ToTable("UTCFGVVL");
        b.Entity<VariableValue>().ToTable("UTCFGVAL");

        // Mirror the original DB2 foreign-key relationships.
        b.Entity<Extent>()
            .HasOne(x => x.Context).WithMany(c => c.Extents)
            .HasForeignKey(x => x.ContextId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<Scope>()
            .HasOne(s => s.Server).WithMany(srv => srv.Scopes)
            .HasForeignKey(s => s.ServerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Scope>()
            .HasOne(s => s.ScopeResolutionMethod).WithMany(m => m.Scopes)
            .HasForeignKey(s => s.ScopeResolutionMethodId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<VariableDefinition>()
            .HasOne(v => v.Extent).WithMany(e => e.VariableDefinitions)
            .HasForeignKey(v => v.ExtentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<VariableDefinition>()
            .HasOne(v => v.VariableType).WithMany(t => t.VariableDefinitions)
            .HasForeignKey(v => v.VariableTypeId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<ValidValue>()
            .HasOne(v => v.VariableDefinition).WithMany(d => d.ValidValues)
            .HasForeignKey(v => v.VariableDefinitionId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<VariableValue>()
            .HasOne(v => v.VariableDefinition).WithMany(d => d.VariableValues)
            .HasForeignKey(v => v.VariableDefinitionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<VariableValue>()
            .HasOne(v => v.Scope).WithMany(s => s.VariableValues)
            .HasForeignKey(v => v.ScopeId).OnDelete(DeleteBehavior.Restrict);

        // GoAnywhere Configuration tables
        b.Entity<GoAnywhereProject>().ToTable("GAPROJECT");
        b.Entity<GoAnywhereConfig>().ToTable("GACONFIG");
        b.Entity<GoAnywhereAuditLog>().ToTable("GAAUDIT");
        
        // IBM i Configuration System audit tables
        b.Entity<IBMiAuditLog>().ToTable("IBMIAUDIT");

        // GoAnywhere relationships
        b.Entity<GoAnywhereConfig>()
            .HasOne(c => c.Project).WithMany(p => p.Configurations)
            .HasForeignKey(c => c.ProjectId).OnDelete(DeleteBehavior.Cascade);

        // Unique constraint on (ProjectId, Environment, ConfigKey)
        b.Entity<GoAnywhereConfig>()
            .HasIndex(c => new { c.ProjectId, c.Environment, c.ConfigKey })
            .IsUnique();

        // Audit lookup and timeline indexes
        b.Entity<GoAnywhereAuditLog>()
            .HasIndex(a => a.ChangedAtUtc);
        b.Entity<GoAnywhereAuditLog>()
            .HasIndex(a => new { a.ProjectName, a.Environment, a.ConfigKey });

        b.Entity<GoAnywhereConfig>()
            .Property(c => c.IsSensitive)
            .HasConversion(value => value ? "Y" : "N", value => value == "Y")
            .HasColumnType("char(1)");
        b.Entity<GoAnywhereAuditLog>()
            .Property(a => a.IsSensitive)
            .HasConversion(value => value ? "Y" : "N", value => value == "Y")
            .HasColumnType("char(1)");
        b.Entity<GoAnywhereAuditLog>()
            .Property(a => a.Id)
            .ValueGeneratedOnAdd();
        b.Entity<GoAnywhereProject>().Property(p => p.CreatedDate).HasColumnType("datetime2");
        b.Entity<GoAnywhereProject>().Property(p => p.LastModifiedDate).HasColumnType("datetime2");
        b.Entity<GoAnywhereConfig>().Property(c => c.CreatedDate).HasColumnType("datetime2");
        b.Entity<GoAnywhereConfig>().Property(c => c.LastModifiedDate).HasColumnType("datetime2");
        b.Entity<GoAnywhereAuditLog>().Property(a => a.ChangedAtUtc).HasColumnType("datetime2");
    }
}
