import { useState, useEffect } from "react";
import { getSource } from "../api";
import "./GoAnywhereConfigPage.css";

interface GAProject {
  id: number;
  name: string;
  description?: string;
  projectPath?: string;
  extentName?: string;
}

interface GAConfig {
  id: number;
  configKey: string;
  configValue?: string;
  description?: string;
  isRequired: boolean;
  isSensitive: boolean;
}

export default function GoAnywhereConfigPage() {
  const [projects, setProjects] = useState<GAProject[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<number | null>(null);
  const [selectedEnvironment, setSelectedEnvironment] = useState<"DATO" | "DATI" | "DATU" | "DATV" | "DATN" | "FCB">(
    getSource() === "FCB" ? "FCB" : "DATO"
  );
  const [configs, setConfigs] = useState<GAConfig[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [configSearch, setConfigSearch] = useState("");
  const [projectSearch, setProjectSearch] = useState("");
  const [showNewProjectForm, setShowNewProjectForm] = useState(false);
  const [showAddParameterForm, setShowAddParameterForm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [newProject, setNewProject] = useState({ name: "", description: "" });
  const [newParameter, setNewParameter] = useState({
    configKey: "",
    configValue: "",
    description: "",
    isRequired: false,
    isSensitive: false,
  });

  const API_BASE = "http://localhost:5000/api";

  // Get environment-specific project path
  const getEnvironmentSpecificPath = (environment: string): string => {
    const productionEnvs = ["FCB", "DATU", "DATV"];
    return productionEnvs.includes(environment) ? "/production" : "/betatest";
  };

  // Load projects on component mount
  useEffect(() => {
    loadProjects();
  }, []);

  // Load configurations when project or environment changes
  useEffect(() => {
    if (selectedProjectId) {
      loadConfigurations(selectedProjectId, selectedEnvironment);
    }
  }, [selectedProjectId, selectedEnvironment]);

  const loadProjects = async () => {
    try {
      setLoading(true);
      setError(undefined);
      const response = await fetch(`${API_BASE}/goanywhere/projects`);
      
      if (!response.ok) {
        throw new Error(`Failed to load projects: ${response.statusText}`);
      }
      
      const data: GAProject[] = await response.json();
      setProjects(data);
      
      // Auto-select first project if available
      if (data.length > 0 && !selectedProjectId) {
        setSelectedProjectId(data[0].id);
      }
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to load projects";
      setError(errorMsg);
      console.error("Error loading projects:", err);
    } finally {
      setLoading(false);
    }
  };

  const loadConfigurations = async (projectId: number, environment: string) => {
    try {
      setLoading(true);
      setError(undefined);
      const response = await fetch(
        `${API_BASE}/goanywhere/configs?projectId=${projectId}&environment=${environment}`
      );
      
      if (!response.ok) {
        throw new Error(`Failed to load configurations (${response.status}): ${response.statusText}`);
      }
      
      const data: GAConfig[] = await response.json();
      
      console.log(`Loaded ${data.length} configurations for project ${projectId}`);
      setConfigs(data);
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to load configurations";
      setError(errorMsg);
      console.error("Error loading configurations:", err);
      // Show empty list on error
      setConfigs([]);
    } finally {
      setLoading(false);
    }
  };

  const handleCreateProject = async () => {
    if (!newProject.name.trim()) {
      setError("Project name is required");
      return;
    }

    try {
      setSaving(true);
      setError(undefined);
      const response = await fetch(`${API_BASE}/goanywhere/projects`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: newProject.name.trim(),
          description: newProject.description.trim() || null,
          projectPath: null,
        }),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `Failed to create project: ${response.statusText}`);
      }

      await loadProjects();
      setShowNewProjectForm(false);
      setNewProject({ name: "", description: "" });
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to create project";
      setError(errorMsg);
      console.error("Error creating project:", err);
    } finally {
      setSaving(false);
    }
  };

  const handleAddParameter = async () => {
    if (!selectedProjectId) {
      setError("Please select a project first");
      return;
    }

    if (!newParameter.configKey.trim()) {
      setError("Parameter name is required");
      return;
    }

    if (!newParameter.description.trim()) {
      setError("Description is required");
      return;
    }

    try {
      setSaving(true);
      setError(undefined);
      const response = await fetch(`${API_BASE}/goanywhere/configs`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          projectId: selectedProjectId,
          environment: selectedEnvironment,
          configKey: newParameter.configKey.trim(),
          configValue: newParameter.configValue.trim() || null,
          description: newParameter.description.trim() || null,
          isRequired: newParameter.isRequired,
          isSensitive: newParameter.isSensitive,
        }),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `Failed to add parameter: ${response.statusText}`);
      }

      await loadConfigurations(selectedProjectId, selectedEnvironment);
      setShowAddParameterForm(false);
      setNewParameter({ configKey: "", configValue: "", description: "", isRequired: false, isSensitive: false });
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to add parameter";
      setError(errorMsg);
      console.error("Error adding parameter:", err);
    } finally {
      setSaving(false);
    }
  };



  const selectedProject = projects.find((p) => p.id === selectedProjectId);
  
  // Filter projects for search
  const filteredProjects = projectSearch.trim()
    ? projects.filter((p) =>
        p.name.toLowerCase().includes(projectSearch.toLowerCase()) ||
        (p.description && p.description.toLowerCase().includes(projectSearch.toLowerCase()))
      )
    : projects;

  // Filter configs for search
  const filteredConfigs = configSearch.trim()
    ? configs.filter((c) =>
        c.configKey.toLowerCase().includes(configSearch.toLowerCase()) ||
        (c.description && c.description.toLowerCase().includes(configSearch.toLowerCase()))
      )
    : configs;

  return (
    <div className="ga-config-container">
      <h2>🔄 GoAnywhere Configuration Management</h2>
      <p className="subtitle">
        View configuration values for GoAnywhere projects. To edit configurations, use the <strong>Parameters</strong> page.
      </p>

      {error && <div className="error-message">{error}</div>}

      <div className="ga-controls">
        <div className="control-group">
          <label htmlFor="project-search">Search Projects:</label>
          <input
            id="project-search"
            type="search"
            placeholder="Search project name..."
            value={projectSearch}
            onChange={(e) => setProjectSearch(e.target.value)}
            className="search-input"
            disabled={loading}
          />
        </div>

        <div className="control-group">
          <label htmlFor="project-select">Select Project:</label>
          <select
            id="project-select"
            value={selectedProjectId || ""}
            onChange={(e) => setSelectedProjectId(parseInt(e.target.value) || null)}
            className="select-field"
            disabled={loading}
          >
            <option value="">-- Choose a Project --</option>
            {filteredProjects.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
                {p.description ? ` - ${p.description}` : ""}
              </option>
            ))}
          </select>
          {projectSearch && <span className="subtle" style={{ marginTop: "4px" }}>{filteredProjects.length} of {projects.length} projects</span>}
        </div>

        <div className="control-group">
          <label>Environment:</label>
          <div className="env-buttons">
            {(getSource() === "FCB" 
              ? (["FCB"] as const)
              : (["DATO", "DATI", "DATU", "DATV", "DATN"] as const)
            ).map((env) => (
              <button
                key={env}
                className={`env-btn ${selectedEnvironment === env ? "active" : ""}`}
                onClick={() => setSelectedEnvironment(env)}
                disabled={loading || !selectedProjectId}
              >
                {env}
              </button>
            ))}
          </div>
        </div>

        <div className="control-group">
          <button
            className="btn-action"
            onClick={() => setShowNewProjectForm(true)}
            disabled={loading || saving}
          >
            ➕ New Project
          </button>
        </div>
      </div>

      {showNewProjectForm && (
        <div className="modal-overlay" onClick={() => setShowNewProjectForm(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <h3>Create New GoAnywhere Project</h3>
            <div className="form-group">
              <label htmlFor="project-name">Project Name *</label>
              <input
                id="project-name"
                type="text"
                placeholder="e.g., MyNewProject"
                value={newProject.name}
                onChange={(e) => setNewProject({ ...newProject, name: e.target.value })}
                className="form-input"
                autoFocus
              />
            </div>
            <div className="form-group">
              <label htmlFor="project-desc">Description</label>
              <input
                id="project-desc"
                type="text"
                placeholder="Optional description"
                value={newProject.description}
                onChange={(e) => setNewProject({ ...newProject, description: e.target.value })}
                className="form-input"
              />
            </div>
            <div className="form-buttons">
              <button
                className="btn-save"
                onClick={handleCreateProject}
                disabled={saving || !newProject.name.trim()}
              >
                {saving ? "Creating..." : "Create Project"}
              </button>
              <button
                className="btn-cancel"
                onClick={() => {
                  setShowNewProjectForm(false);
                  setNewProject({ name: "", description: "" });
                }}
                disabled={saving}
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}

      {selectedProjectId && selectedProject && (
        <div className="ga-config-view">
          <div className="project-header">
            <h3>{selectedProject.name}</h3>
            {selectedProject.description && <p>{selectedProject.description}</p>}
            <p className="project-path">
              <strong>Path:</strong> {getEnvironmentSpecificPath(selectedEnvironment)}
            </p>
            <span className="environment-badge">{selectedEnvironment}</span>
          </div>

          {loading ? (
            <div className="loading">Loading configurations...</div>
          ) : (
            <>
              {configs.length > 0 && (
                <div className="search-bar">
                  <input
                    type="search"
                    placeholder="Search configurations by parameter..."
                    value={configSearch}
                    onChange={(e) => setConfigSearch(e.target.value)}
                    className="search"
                  />
                  <span className="subtle">
                    {filteredConfigs.length} of {configs.length} configs
                  </span>
                </div>
              )}
              {filteredConfigs.length === 0 ? (
                <div className="no-configs">
                  <p>{configSearch ? "No matching configurations found." : "No configurations found for this project and environment."}</p>
                  <p className="hint">📊 To manage configurations, use the <strong>Parameters</strong> page</p>
                </div>
              ) : (
                <table className="config-table">
                  <thead>
                    <tr>
                      <th>Parameter</th>
                      <th>Value</th>
                      <th>Description</th>
                      <th>Required</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredConfigs.map((config) => (
                      <tr key={config.id} className={config.isSensitive ? "sensitive-row" : ""}>
                        <td className="key-column">
                          <strong>{config.configKey}</strong>
                        </td>
                        <td className="value-column">
                          <code>{config.isSensitive ? (config.configValue ? "●●●●●●●●" : "(empty)") : config.configValue || "(empty)"}</code>
                        </td>
                        <td className="description-column">{config.description || "-"}</td>
                        <td className="required-column">
                          {config.isRequired ? <span className="badge-required">Yes</span> : "-"}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </>
          )}

          <div className="parameter-actions">
            <button
              className="btn-add-param"
              onClick={() => setShowAddParameterForm(true)}
              disabled={saving || loading}
            >
              ➕ Add Parameter
            </button>
          </div>

          {configs.length > 0 && (
            <div className="config-stats">
              <p>
                <strong>Total Configurations:</strong> {configs.length} |{" "}
                <strong>Required:</strong> {configs.filter((c) => c.isRequired).length} |{" "}
                <strong>Sensitive:</strong> {configs.filter((c) => c.isSensitive).length}
              </p>
            </div>
          )}

          {showAddParameterForm && (
            <div className="modal-overlay" onClick={() => setShowAddParameterForm(false)}>
              <div className="modal-content" onClick={(e) => e.stopPropagation()}>
                <h3>Add Parameter to {selectedProject?.name}</h3>
                <p className="form-subtitle">Environment: <strong>{selectedEnvironment}</strong></p>
                <div className="form-group">
                  <label htmlFor="param-key">Parameter Name *</label>
                  <input
                    id="param-key"
                    type="text"
                    placeholder="e.g., ApiKey, DatabaseUrl"
                    value={newParameter.configKey}
                    onChange={(e) => setNewParameter({ ...newParameter, configKey: e.target.value })}
                    className="form-input"
                    autoFocus
                  />
                </div>
                <div className="form-group">
                  <label htmlFor="param-value">Value</label>
                  <input
                    id="param-value"
                    type={newParameter.isSensitive ? "password" : "text"}
                    placeholder="Enter value (optional)"
                    value={newParameter.configValue}
                    onChange={(e) => setNewParameter({ ...newParameter, configValue: e.target.value })}
                    className="form-input"
                  />
                </div>
                <div className="form-group">
                  <label htmlFor="param-desc">Description *</label>
                  <input
                    id="param-desc"
                    type="text"
                    placeholder="Brief description"
                    value={newParameter.description}
                    onChange={(e) => setNewParameter({ ...newParameter, description: e.target.value })}
                    className="form-input"
                  />
                </div>
                <div className="form-group-checkbox">
                  <label htmlFor="param-required">
                    <input
                      id="param-required"
                      type="checkbox"
                      checked={newParameter.isRequired}
                      onChange={(e) => setNewParameter({ ...newParameter, isRequired: e.target.checked })}
                    />
                    {" "}Required
                  </label>
                  <label htmlFor="param-sensitive">
                    <input
                      id="param-sensitive"
                      type="checkbox"
                      checked={newParameter.isSensitive}
                      onChange={(e) => setNewParameter({ ...newParameter, isSensitive: e.target.checked })}
                    />
                    {" "}Sensitive (password, key, token, etc.)
                  </label>
                </div>
                <div className="form-buttons">
                  <button
                    className="btn-save"
                    onClick={handleAddParameter}
                    disabled={saving || !newParameter.configKey.trim() || !newParameter.description.trim()}
                  >
                    {saving ? "Adding..." : "Add Parameter"}
                  </button>
                  <button
                    className="btn-cancel"
                    onClick={() => {
                      setShowAddParameterForm(false);
                      setNewParameter({ configKey: "", configValue: "", description: "", isRequired: false, isSensitive: false });
                    }}
                    disabled={saving}
                  >
                    Cancel
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
