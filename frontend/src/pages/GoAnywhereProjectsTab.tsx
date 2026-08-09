import { useState } from "react";
import { getSource } from "../api";
import type {
  GAProject,
  GAConfig,
  AvailableParameter,
  Environment,
  NewProjectState,
  NewParameterState,
} from "./goanywhereTypes";

interface ProjectsTabProps {
  loading: boolean;
  saving: boolean;
  projects: GAProject[];
  filteredProjects: GAProject[];
  projectSearch: string;
  setProjectSearch: (value: string) => void;
  selectedProjectId: number | null;
  setSelectedProjectId: (id: number | null) => void;
  selectedProject: GAProject | undefined;
  selectedEnvironment: Environment | null;
  setSelectedEnvironment: (env: Environment | null) => void;
  configs: GAConfig[];
  filteredConfigs: GAConfig[];
  configSearch: string;
  setConfigSearch: (value: string) => void;
  getEnvironmentSpecificPath: (environment: string) => string;
  showNewProjectForm: boolean;
  setShowNewProjectForm: (value: boolean) => void;
  newProject: NewProjectState;
  setNewProject: (value: NewProjectState) => void;
  handleCreateProject: () => void;
  showAddParameterForm: boolean;
  setShowAddParameterForm: (value: boolean) => void;
  newParameter: NewParameterState;
  setNewParameter: (value: NewParameterState) => void;
  handleAddParameter: () => void;
  handleUpdateProjectConfig: (configId: number, configValue: string) => Promise<void>;
  handleDeleteProjectConfig: (configId: number) => Promise<void>;
  availableParameters: AvailableParameter[];
}

export default function ProjectsTab({
  loading,
  saving,
  projects,
  filteredProjects,
  projectSearch,
  setProjectSearch,
  selectedProjectId,
  setSelectedProjectId,
  selectedProject,
  selectedEnvironment,
  setSelectedEnvironment,
  configs,
  filteredConfigs,
  configSearch,
  setConfigSearch,
  getEnvironmentSpecificPath,
  showNewProjectForm,
  setShowNewProjectForm,
  newProject,
  setNewProject,
  handleCreateProject,
  showAddParameterForm,
  setShowAddParameterForm,
  newParameter,
  setNewParameter,
  handleAddParameter,
  handleUpdateProjectConfig,
  handleDeleteProjectConfig,
  availableParameters,
}: ProjectsTabProps) {
  const [editingConfigId, setEditingConfigId] = useState<number | null>(null);
  const [editValue, setEditValue] = useState("");
  const [actionConfigId, setActionConfigId] = useState<number | null>(null);

  const startEdit = (config: GAConfig) => {
    setEditingConfigId(config.id);
    setEditValue(config.configValue || "");
  };

  const cancelEdit = () => {
    setEditingConfigId(null);
    setEditValue("");
  };

  const saveEdit = async (configId: number) => {
    setActionConfigId(configId);
    try {
      await handleUpdateProjectConfig(configId, editValue);
      cancelEdit();
    } finally {
      setActionConfigId(null);
    }
  };

  const deleteConfig = async (configId: number, configKey: string) => {
    const confirmed = window.confirm(`Delete parameter '${configKey}' from this project/environment?`);
    if (!confirmed) return;

    setActionConfigId(configId);
    try {
      await handleDeleteProjectConfig(configId);
    } finally {
      setActionConfigId(null);
    }
  };

  const environments: Environment[] =
    getSource() === "FCB"
      ? ["FCB"]
      : ["DATO", "DATI", "DATU", "DATV", "DATN"];

  // Only parameters not already added to this project/environment can be selected
  const existingKeys = new Set(configs.map((c) => c.configKey));
  const selectableParameters = availableParameters.filter(
    (p) => !existingKeys.has(p.configKey)
  );

  return (
    <div>
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
          {projectSearch && (
            <span className="subtle" style={{ marginTop: "4px" }}>
              {filteredProjects.length} of {projects.length} projects
            </span>
          )}
        </div>

        <div className="control-group">
          <label>Environment:</label>
          <div className="env-buttons">
            {environments.map((env) => (
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
          {!selectedEnvironment && selectedProjectId && (
            <span className="subtle" style={{ marginTop: "4px" }}>
              Select an environment to load project parameters.
            </span>
          )}
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
              <strong>Path:</strong> {selectedEnvironment ? getEnvironmentSpecificPath(selectedEnvironment) : "-"}
            </p>
            {selectedEnvironment && <span className="environment-badge">{selectedEnvironment}</span>}
          </div>

          {!selectedEnvironment ? (
            <div className="no-configs">
              <p>Select an environment to view project-level parameters.</p>
            </div>
          ) : loading ? (
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
                  <p>
                    {configSearch
                      ? "No matching configurations found."
                      : "No configurations found for this project and environment."}
                  </p>
                  <p className="hint">
                    📊 To manage configurations, use the <strong>Parameters</strong> page
                  </p>
                </div>
              ) : (
                <table className="config-table">
                  <thead>
                    <tr>
                      <th>Parameter</th>
                      <th>Value</th>
                      <th>Description</th>
                      <th>Required</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredConfigs.map((config) => (
                      <tr key={config.id} className={config.isSensitive ? "sensitive-row" : ""}>
                        <td className="key-column">
                          <strong>{config.configKey}</strong>
                        </td>
                        <td className="value-column">
                          {editingConfigId === config.id ? (
                            <input
                              type="text"
                              value={editValue}
                              onChange={(e) => setEditValue(e.target.value)}
                              className="inline-edit-input"
                              placeholder="Enter value"
                            />
                          ) : (
                            <code>
                              {config.configValue || "(empty)"}
                            </code>
                          )}
                        </td>
                        <td className="description-column">{config.description || "-"}</td>
                        <td className="required-column">
                          {config.isRequired ? <span className="badge-required">Yes</span> : "-"}
                        </td>
                        <td className="actions-column">
                          {editingConfigId === config.id ? (
                            <>
                              <button
                                className="btn-row-action btn-row-save"
                                onClick={() => saveEdit(config.id)}
                                disabled={saving || actionConfigId === config.id}
                              >
                                {actionConfigId === config.id ? "Saving..." : "Save"}
                              </button>
                              <button
                                className="btn-row-action btn-row-cancel"
                                onClick={cancelEdit}
                                disabled={saving || actionConfigId === config.id}
                              >
                                Cancel
                              </button>
                            </>
                          ) : (
                            <>
                              <button
                                className="btn-row-action"
                                onClick={() => startEdit(config)}
                                disabled={saving || actionConfigId === config.id}
                              >
                                Edit
                              </button>
                              <button
                                className="btn-row-action btn-row-delete"
                                onClick={() => deleteConfig(config.id, config.configKey)}
                                disabled={saving || actionConfigId === config.id}
                              >
                                {actionConfigId === config.id ? "Deleting..." : "Delete"}
                              </button>
                            </>
                          )}
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
              disabled={saving || loading || !selectedEnvironment}
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
                <p className="form-subtitle">
                  Environment: <strong>{selectedEnvironment}</strong>
                </p>
                <div className="form-group">
                  <label htmlFor="param-key">Parameter *</label>
                  {selectableParameters.length === 0 ? (
                    <p className="hint">
                      No available parameters to add. All existing parameters for{" "}
                      <strong>{selectedEnvironment}</strong> are already added to this project.
                    </p>
                  ) : (
                    <select
                      id="param-key"
                      value={newParameter.configKey}
                      onChange={(e) => {
                        const param = selectableParameters.find(
                          (p) => p.configKey === e.target.value
                        );
                        if (param) {
                          setNewParameter({
                            configKey: param.configKey,
                            configValue: "",
                            description: "",
                            isRequired: false,
                            isSensitive: param.isSensitive,
                          });
                        } else {
                          setNewParameter({
                            configKey: "",
                            configValue: "",
                            description: "",
                            isRequired: false,
                            isSensitive: false,
                          });
                        }
                      }}
                      className="form-input"
                      autoFocus
                    >
                      <option value="">-- Select a parameter --</option>
                      {selectableParameters.map((p) => (
                        <option key={p.configKey} value={p.configKey}>
                          {p.configKey}
                          {p.projectCount ? ` (used in ${p.projectCount} project${p.projectCount === 1 ? "" : "s"})` : ""}
                        </option>
                      ))}
                    </select>
                  )}
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
                    disabled={!newParameter.configKey}
                  />
                </div>
                <div className="form-group">
                  <label htmlFor="param-desc">Description *</label>
                  <input
                    id="param-desc"
                    type="text"
                    placeholder="Describe this parameter"
                    value={newParameter.description}
                    onChange={(e) => setNewParameter({ ...newParameter, description: e.target.value })}
                    className="form-input"
                    disabled={!newParameter.configKey}
                  />
                </div>
                <div className="form-group-checkbox">
                  <label htmlFor="param-required">
                    <input
                      id="param-required"
                      type="checkbox"
                      checked={newParameter.isRequired}
                      onChange={(e) => setNewParameter({ ...newParameter, isRequired: e.target.checked })}
                      disabled={!newParameter.configKey}
                    />
                    {" "}Required
                  </label>
                  <label htmlFor="param-sensitive">
                    <input
                      id="param-sensitive"
                      type="checkbox"
                      checked={newParameter.isSensitive}
                      onChange={(e) => setNewParameter({ ...newParameter, isSensitive: e.target.checked })}
                      disabled={!newParameter.configKey}
                    />
                    {" "}Sensitive (password, key, token, etc.)
                  </label>
                </div>
                <div className="form-buttons">
                  <button
                    className="btn-save"
                    onClick={handleAddParameter}
                    disabled={saving || !newParameter.configKey.trim()}
                  >
                    {saving ? "Adding..." : "Add Parameter"}
                  </button>
                  <button
                    className="btn-cancel"
                    onClick={() => {
                      setShowAddParameterForm(false);
                      setNewParameter({
                        configKey: "",
                        configValue: "",
                        description: "",
                        isRequired: false,
                        isSensitive: false,
                      });
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
