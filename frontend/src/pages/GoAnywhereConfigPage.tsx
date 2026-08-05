import { useState, useEffect } from "react";
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
  const [selectedEnvironment, setSelectedEnvironment] = useState<"DATO" | "DATI" | "DATU" | "DATV" | "DATN" | "FCB">("DATO");
  const [configs, setConfigs] = useState<GAConfig[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editValue, setEditValue] = useState<string>("");
  const [saving, setSaving] = useState(false);
  const [editingProjectPath, setEditingProjectPath] = useState(false);
  const [editProjectPath, setEditProjectPath] = useState<string>("");
  const [showAddForm, setShowAddForm] = useState(false);
  const [newConfig, setNewConfig] = useState({
    configKey: "",
    configValue: "",
    description: "",
    isRequired: false,
    isSensitive: false,
  });
  const [showCreateProjectForm, setShowCreateProjectForm] = useState(false);
  const [newProject, setNewProject] = useState({
    name: "",
    description: "",
    projectPath: "",
  });

  const API_BASE = "http://localhost:5198/api";

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
        throw new Error(`Failed to load configurations: ${response.statusText}`);
      }
      
      const data: GAConfig[] = await response.json();
      setConfigs(data);
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to load configurations";
      setError(errorMsg);
      console.error("Error loading configurations:", err);
    } finally {
      setLoading(false);
    }
  };

  const handleEdit = (configId: number, value: string | undefined) => {
    setEditingId(configId);
    setEditValue(value || "");
  };

  const handleSave = async (configId: number) => {
    if (!editValue) {
      setError("Configuration value cannot be empty");
      return;
    }

    try {
      setSaving(true);
      const response = await fetch(`${API_BASE}/goanywhere/configs/${configId}`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ configValue: editValue }),
      });

      if (!response.ok) {
        throw new Error(`Failed to save configuration: ${response.statusText}`);
      }

      // Reload configurations to get updated data
      if (selectedProjectId) {
        await loadConfigurations(selectedProjectId, selectedEnvironment);
      }
      setEditingId(null);
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to save configuration";
      setError(errorMsg);
      console.error("Error saving configuration:", err);
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = () => {
    setEditingId(null);
  };

  const handleEditProjectPath = () => {
    setEditingProjectPath(true);
    setEditProjectPath(selectedProject?.projectPath || "");
  };

  const handleSaveProjectPath = async () => {
    if (!selectedProjectId) return;

    try {
      setSaving(true);
      
      // Ensure project path starts with '/' if provided
      let projectPath = editProjectPath || null;
      if (projectPath && !projectPath.startsWith('/')) {
        projectPath = '/' + projectPath;
      }

      const response = await fetch(`${API_BASE}/goanywhere/projects/${selectedProjectId}/path`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ projectPath: projectPath }),
      });

      if (!response.ok) {
        throw new Error(`Failed to save project path: ${response.statusText}`);
      }

      // Update the projects list with new path
      const updatedProjects = projects.map((p) =>
        p.id === selectedProjectId ? { ...p, projectPath: projectPath || undefined } : p
      );
      setProjects(updatedProjects);
      setEditingProjectPath(false);
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to save project path";
      setError(errorMsg);
      console.error("Error saving project path:", err);
    } finally {
      setSaving(false);
    }
  };

  const handleCancelProjectPath = () => {
    setEditingProjectPath(false);
  };

  const handleAddConfiguration = async () => {
    if (!newConfig.configKey) {
      setError("Configuration key is required");
      return;
    }

    if (!selectedProjectId) {
      setError("Please select a project first");
      return;
    }

    try {
      setSaving(true);
      const response = await fetch(`${API_BASE}/goanywhere/configs`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          projectId: selectedProjectId,
          environment: selectedEnvironment,
          configKey: newConfig.configKey,
          configValue: newConfig.configValue,
          description: newConfig.description,
          isRequired: newConfig.isRequired,
          isSensitive: newConfig.isSensitive,
        }),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || `Failed to create configuration: ${response.statusText}`);
      }

      // Reload configurations to include the new one
      await loadConfigurations(selectedProjectId, selectedEnvironment);
      
      // Reset form
      setNewConfig({
        configKey: "",
        configValue: "",
        description: "",
        isRequired: false,
        isSensitive: false,
      });
      setShowAddForm(false);
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to create configuration";
      setError(errorMsg);
      console.error("Error creating configuration:", err);
    } finally {
      setSaving(false);
    }
  };

  const handleCancelAdd = () => {
    setShowAddForm(false);
    setNewConfig({
      configKey: "",
      configValue: "",
      description: "",
      isRequired: false,
      isSensitive: false,
    });
  };

  const handleDeleteConfiguration = async (configId: number, configKey: string) => {
    if (!window.confirm(`Are you sure you want to delete the configuration "${configKey}"?`)) {
      return;
    }

    try {
      setSaving(true);
      const response = await fetch(`${API_BASE}/goanywhere/configs/${configId}`, {
        method: "DELETE",
        headers: {
          "Content-Type": "application/json",
        },
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || `Failed to delete configuration: ${response.statusText}`);
      }

      // Reload configurations to remove the deleted one
      if (selectedProjectId) {
        await loadConfigurations(selectedProjectId, selectedEnvironment);
      }
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to delete configuration";
      setError(errorMsg);
      console.error("Error deleting configuration:", err);
    } finally {
      setSaving(false);
    }
  };

  const handleCreateProject = async () => {
    if (!newProject.name) {
      setError("Project name is required");
      return;
    }

    try {
      setSaving(true);
      
      // Ensure project path starts with '/' if provided
      let projectPath = newProject.projectPath || null;
      if (projectPath && !projectPath.startsWith('/')) {
        projectPath = '/' + projectPath;
      }

      const response = await fetch(`${API_BASE}/goanywhere/projects`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          name: newProject.name,
          description: newProject.description || null,
          projectPath: projectPath,
        }),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || `Failed to create project: ${response.statusText}`);
      }

      // Reload projects to include the new one
      await loadProjects();

      // Reset form and select the new project
      setNewProject({
        name: "",
        description: "",
        projectPath: "",
      });
      setShowCreateProjectForm(false);
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to create project";
      setError(errorMsg);
      console.error("Error creating project:", err);
    } finally {
      setSaving(false);
    }
  };

  const handleCancelCreateProject = () => {
    setShowCreateProjectForm(false);
    setNewProject({
      name: "",
      description: "",
      projectPath: "",
    });
  };

  const selectedProject = projects.find((p) => p.id === selectedProjectId);

  return (
    <div className="ga-config-container">
      <h2>GoAnywhere Configuration Management</h2>
      <p className="subtitle">
        Manage configuration values for GoAnywhere projects across DATO, DATI, DATU, DATV, DATN, and FCB environments.
      </p>

      {error && <div className="error-message">{error}</div>}

      <div className="ga-controls">
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
            {projects.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
                {p.description ? ` - ${p.description}` : ""}
              </option>
            ))}
          </select>
        </div>

        <div className="control-group">
          <label>Environment:</label>
          <div className="env-buttons">
            {(["DATO", "DATI", "DATU", "DATV", "DATN", "FCB"] as const).map((env) => (
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
            className="btn-new-project"
            onClick={() => setShowCreateProjectForm(true)}
            disabled={loading || saving}
          >
            + New Project
          </button>
        </div>
      </div>

      {showCreateProjectForm && (
        <div className="create-project-form">
          <h3>Create New Project</h3>
          <div className="form-group">
            <label htmlFor="project-name">Project Name *</label>
            <input
              id="project-name"
              type="text"
              placeholder="e.g., APClearedChecks, TestAPI"
              value={newProject.name}
              onChange={(e) => setNewProject({ ...newProject, name: e.target.value })}
              className="form-input"
              autoFocus
            />
          </div>
          <div className="form-group">
            <label htmlFor="project-description">Description</label>
            <input
              id="project-description"
              type="text"
              placeholder="Brief description of the project"
              value={newProject.description}
              onChange={(e) => setNewProject({ ...newProject, description: e.target.value })}
              className="form-input"
            />
          </div>
          <div className="form-group">
            <label htmlFor="project-path">Project Path</label>
            <input
              id="project-path"
              type="text"
              placeholder="e.g., /dev/Ajay, /IRS (starts with /)"
              value={newProject.projectPath}
              onChange={(e) => setNewProject({ ...newProject, projectPath: e.target.value })}
              className="form-input"
            />
            <small style={{ color: "#7f8c8d", marginTop: "-4px" }}>
              Optional. Paths should start with / (will be auto-corrected if needed)
            </small>
          </div>
          <div className="form-buttons">
            <button
              className="btn-save"
              onClick={handleCreateProject}
              disabled={saving || !newProject.name}
            >
              {saving ? "Creating..." : "Create Project"}
            </button>
            <button
              className="btn-cancel"
              onClick={handleCancelCreateProject}
              disabled={saving}
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      {selectedProjectId && selectedProject && (
        <div className="ga-config-view">
          <div className="project-header">
            <h3>{selectedProject.name}</h3>
            {selectedProject.description && <p>{selectedProject.description}</p>}
            <div className="project-path-section">
              {editingProjectPath ? (
                <div className="project-path-edit">
                  <label>Path:</label>
                  <input
                    type="text"
                    value={editProjectPath}
                    onChange={(e) => setEditProjectPath(e.target.value)}
                    className="edit-input"
                    placeholder="(empty)"
                    autoFocus
                  />
                  <button
                    className="btn-save"
                    onClick={handleSaveProjectPath}
                    disabled={saving}
                  >
                    {saving ? "Saving..." : "Save"}
                  </button>
                  <button
                    className="btn-cancel"
                    onClick={handleCancelProjectPath}
                    disabled={saving}
                  >
                    Cancel
                  </button>
                </div>
              ) : (
                <p className="project-path">
                  <strong>Path:</strong> {selectedProject.projectPath || "(not configured)"}
                  <button
                    className="btn-edit-path"
                    onClick={handleEditProjectPath}
                    disabled={saving}
                  >
                    Edit
                  </button>
                </p>
              )}
            </div>
            <span className="environment-badge">{selectedEnvironment}</span>
          </div>

          {loading ? (
            <div className="loading">Loading configurations...</div>
          ) : configs.length === 0 ? (
            <div className="no-configs">No configurations found for this project and environment.</div>
          ) : (
            <table className="config-table">
              <thead>
                <tr>
                  <th>Parameter</th>
                  <th>Value</th>
                  <th>Description</th>
                  <th>Required</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {configs.map((config) => (
                  <tr key={config.id} className={config.isSensitive ? "sensitive-row" : ""}>
                    <td className="key-column">
                      <strong>{config.configKey}</strong>
                    </td>
                    <td className="value-column">
                      {editingId === config.id ? (
                        <input
                          type={config.isSensitive ? "password" : "text"}
                          value={editValue}
                          onChange={(e) => setEditValue(e.target.value)}
                          className="edit-input"
                          autoFocus
                        />
                      ) : (
                        <code>{config.isSensitive ? "●●●●●●●●" : config.configValue || "(empty)"}</code>
                      )}
                    </td>
                    <td className="description-column">{config.description || "-"}</td>
                    <td className="required-column">
                      {config.isRequired ? <span className="badge-required">Yes</span> : "-"}
                    </td>
                    <td className="action-column">
                      {editingId === config.id ? (
                        <>
                          <button
                            className="btn-save"
                            onClick={() => handleSave(config.id)}
                            disabled={saving}
                          >
                            {saving ? "Saving..." : "Save"}
                          </button>
                          <button
                            className="btn-cancel"
                            onClick={handleCancel}
                            disabled={saving}
                          >
                            Cancel
                          </button>
                        </>
                      ) : (
                        <>
                          <button
                            className="btn-edit"
                            onClick={() => handleEdit(config.id, config.configValue)}
                            disabled={saving}
                          >
                            Edit
                          </button>
                          <button
                            className="btn-delete"
                            onClick={() => handleDeleteConfiguration(config.id, config.configKey)}
                            disabled={saving}
                          >
                            Delete
                          </button>
                        </>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {!showAddForm && selectedProjectId && (
            <div className="add-config-button-container">
              <button
                className="btn-add-config"
                onClick={() => setShowAddForm(true)}
                disabled={saving}
              >
                + Add Configuration
              </button>
            </div>
          )}

          {showAddForm && (
            <div className="add-config-form">
              <h4>Add New Configuration</h4>
              <div className="form-group">
                <label htmlFor="new-key">Parameter Name *</label>
                <input
                  id="new-key"
                  type="text"
                  placeholder="e.g., ApiKey, DatabaseUrl"
                  value={newConfig.configKey}
                  onChange={(e) => setNewConfig({ ...newConfig, configKey: e.target.value })}
                  className="form-input"
                  autoFocus
                />
              </div>
              <div className="form-group">
                <label htmlFor="new-value">Value</label>
                <input
                  id="new-value"
                  type={newConfig.isSensitive ? "password" : "text"}
                  placeholder="Enter the configuration value"
                  value={newConfig.configValue}
                  onChange={(e) => setNewConfig({ ...newConfig, configValue: e.target.value })}
                  className="form-input"
                />
              </div>
              <div className="form-group">
                <label htmlFor="new-description">Description</label>
                <input
                  id="new-description"
                  type="text"
                  placeholder="Brief description of this configuration"
                  value={newConfig.description}
                  onChange={(e) => setNewConfig({ ...newConfig, description: e.target.value })}
                  className="form-input"
                />
              </div>
              <div className="form-group-checkbox">
                <label htmlFor="new-required">
                  <input
                    id="new-required"
                    type="checkbox"
                    checked={newConfig.isRequired}
                    onChange={(e) => setNewConfig({ ...newConfig, isRequired: e.target.checked })}
                  />
                  {" "}Required
                </label>
                <label htmlFor="new-sensitive">
                  <input
                    id="new-sensitive"
                    type="checkbox"
                    checked={newConfig.isSensitive}
                    onChange={(e) => setNewConfig({ ...newConfig, isSensitive: e.target.checked })}
                  />
                  {" "}Sensitive (password, key, token, etc.)
                </label>
              </div>
              <div className="form-buttons">
                <button
                  className="btn-save"
                  onClick={handleAddConfiguration}
                  disabled={saving || !newConfig.configKey}
                >
                  {saving ? "Creating..." : "Create Configuration"}
                </button>
                <button
                  className="btn-cancel"
                  onClick={handleCancelAdd}
                  disabled={saving}
                >
                  Cancel
                </button>
              </div>
            </div>
          )}

          {configs.length > 0 && (
            <div className="config-stats">
              <p>
                <strong>Total Configurations:</strong> {configs.length} |{" "}
                <strong>Required:</strong> {configs.filter((c) => c.isRequired).length} |{" "}
                <strong>Sensitive:</strong> {configs.filter((c) => c.isSensitive).length}
              </p>
            </div>
          )}
        </div>
      )}

      {!selectedProjectId && !loading && (
        <div className="no-project">
          <p>Please select a project to view and edit its configuration.</p>
        </div>
      )}

      <div className="ga-info">
        <h4>Quick Reference</h4>
        <ul>
          <li>🔒 Sensitive fields contain passwords, API keys, and encryption credentials</li>
          <li>✓ Required fields must have values before deployment</li>
          <li>DATO - Development/Test O environment</li>
          <li>DATI - Development/Test I environment</li>
          <li>DATU - Development/Test U environment</li>
          <li>DATV - Development/Test V environment</li>
          <li>DATN - Development/Test N environment</li>
          <li>FCB - File Control Board environment</li>
        </ul>
      </div>
    </div>
  );
}
