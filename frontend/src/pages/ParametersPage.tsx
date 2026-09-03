import { useState, useEffect } from 'react';
import { getIsAdmin } from '../api';
import '../pages/ParametersPage.css';

const API_BASE = '/api';

interface Parameter {
  configKey: string;
  environment: string;
  projectCount: number;
  sampleValue: string;
  isSensitive: boolean;
}

interface ParameterDetail {
  configKey: string;
  environment: string;
  isSensitive: boolean;
  isRequired: boolean;
  description: string;
  projectCount: number;
  configurations: Array<{
    id: number;
    projectId: number;
    projectName: string;
    environment?: string;
    projectPath: string;
    configValue: string;
    actualValue: string;
    isSensitive: boolean;
  }>;
}

interface EnvironmentPreview {
  environment: string;
  projectCount: number;
  currentValue: string;
  found: boolean;
  valueBreakdown: Array<{
    value: string;
    count: number;
  }>;
}

export default function ParametersPage() {
  const [parameters, setParameters] = useState<Parameter[]>([]);
  const [selectedEnvironment, setSelectedEnvironment] = useState<string>('');
  const [selectedEnvironments, setSelectedEnvironments] = useState<Set<string>>(new Set());
  const [selectedParameter, setSelectedParameter] = useState<Parameter | null>(null);
  const [parameterDetails, setParameterDetails] = useState<ParameterDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [editValue, setEditValue] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [environments, setEnvironments] = useState<string[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [bulkUpdateMode, setBulkUpdateMode] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [justUpdated, setJustUpdated] = useState(false);
  const [environmentPreviews, setEnvironmentPreviews] = useState<EnvironmentPreview[]>([]);
  const [isPreviewLoading, setIsPreviewLoading] = useState(false);
  const [showEnvPreview, setShowEnvPreview] = useState(false);
  const [selectedEnvConfigurations, setSelectedEnvConfigurations] = useState<ParameterDetail['configurations']>([]);
  const [isSelectedEnvConfigsLoading, setIsSelectedEnvConfigsLoading] = useState(false);
  const [editingConfigId, setEditingConfigId] = useState<number | null>(null);
  const [rowEditValue, setRowEditValue] = useState('');
  const [rowActionConfigId, setRowActionConfigId] = useState<number | null>(null);

  // Load available environments
  useEffect(() => {
    loadEnvironments();
  }, []);

  // Load parameters when environment changes
  useEffect(() => {
    if (selectedEnvironment) {
      loadParameters();
    } else {
      setParameters([]);
      setSelectedParameter(null);
      setParameterDetails(null);
      setEditValue('');
    }
  }, [selectedEnvironment]);

  // Initialize selectedEnvironments when environments load
  useEffect(() => {
    if (selectedEnvironment && selectedEnvironments.size === 0) {
      setSelectedEnvironments(new Set([selectedEnvironment]));
    }
  }, [selectedEnvironment]);

  const loadEnvironments = async () => {
    try {
      const response = await fetch(`${API_BASE}/goanywhere/parameters`);
      if (response.ok) {
        const data: Parameter[] = await response.json();
        const envs = [...new Set(data.map((p: Parameter) => p.environment))].sort() as string[];
        setEnvironments(envs);
      }
    } catch (error) {
      console.error('Error loading environments:', error);
    }
  };

  const handleSelectEnvironment = (env: string) => {
    setSelectedEnvironment(env);
    if (!bulkUpdateMode) {
      setSelectedEnvironments(new Set([env]));
    }
  };

  const loadParameters = async () => {
    setLoading(true);
    try {
      const response = await fetch(`${API_BASE}/goanywhere/parameters?environment=${selectedEnvironment}`);
      if (response.ok) {
        const data = await response.json();
        setParameters(data);
        setSelectedParameter(null);
        setParameterDetails(null);
        setEditValue('');
      } else {
        setMessage({ type: 'error', text: 'Failed to load parameters' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: `Error: ${error}` });
    } finally {
      setLoading(false);
    }
  };

  const handleParameterSelect = async (param: Parameter) => {
    setSelectedParameter(param);
    setShowEnvPreview(false);
    setEnvironmentPreviews([]);
    setLoading(true);
    try {
      const response = await fetch(`${API_BASE}/goanywhere/parameters/${param.configKey}/${param.environment}`);
      if (response.ok) {
        const data = await response.json();
        setParameterDetails(data);
        setEditValue(data.configurations[0]?.actualValue || data.sampleValue || '');
      } else {
        setMessage({ type: 'error', text: 'Failed to load parameter details' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: `Error: ${error}` });
    } finally {
      setLoading(false);
    }
  };

  const getEnvironmentPreviewSummary = (detail: ParameterDetail) => {
    const valueCountMap = new Map<string, number>();
    for (const config of detail.configurations) {
      const value = (config.actualValue ?? '').trim();
      if (!value) continue;
      valueCountMap.set(value, (valueCountMap.get(value) ?? 0) + 1);
    }

    const valueBreakdown = Array.from(valueCountMap.entries())
      .map(([value, count]) => ({ value, count }))
      .sort((a, b) => b.count - a.count);

    if (valueBreakdown.length === 0) {
      return {
        currentValue: '—',
        valueBreakdown
      };
    }

    if (valueBreakdown.length === 1) {
      return {
        currentValue: valueBreakdown[0].value,
        valueBreakdown
      };
    }

    return {
      currentValue: `Mixed (${valueBreakdown.length} values)`,
      valueBreakdown
    };
  };

  const handleShowEnvironmentPreview = async () => {
    if (!selectedParameter) {
      setMessage({ type: 'error', text: 'Select a parameter first' });
      return;
    }

    if (selectedEnvironments.size === 0) {
      setMessage({ type: 'error', text: 'Select at least one environment to preview current values' });
      return;
    }

    setIsPreviewLoading(true);
    setShowEnvPreview(true);

    try {
      const selected = Array.from(selectedEnvironments).sort();
      const previews = await Promise.all(selected.map(async (env): Promise<EnvironmentPreview> => {
        const response = await fetch(`${API_BASE}/goanywhere/parameters/${selectedParameter.configKey}/${env}`);

        if (response.status === 404) {
          return {
            environment: env,
            projectCount: 0,
            currentValue: 'Not configured',
            found: false,
            valueBreakdown: []
          };
        }

        if (!response.ok) {
          throw new Error(`Failed to load current value for ${env}`);
        }

        const data: ParameterDetail = await response.json();
        const previewSummary = getEnvironmentPreviewSummary(data);

        return {
          environment: env,
          projectCount: data.projectCount,
          currentValue: previewSummary.currentValue,
          found: true,
          valueBreakdown: previewSummary.valueBreakdown
        };
      }));

      setEnvironmentPreviews(previews);
    } catch (error) {
      setMessage({ type: 'error', text: `Error loading environment preview: ${error}` });
      setShowEnvPreview(false);
      setEnvironmentPreviews([]);
    } finally {
      setIsPreviewLoading(false);
    }
  };

  const loadSelectedEnvironmentConfigurations = async (configKey: string) => {
    if (selectedEnvironments.size === 0) {
      setSelectedEnvConfigurations([]);
      return;
    }

    setIsSelectedEnvConfigsLoading(true);
    try {
      const selected = Array.from(selectedEnvironments).sort();
      const detailResponses = await Promise.all(selected.map(async (env) => {
        const response = await fetch(`${API_BASE}/goanywhere/parameters/${configKey}/${env}`);
        if (response.status === 404) return [];
        if (!response.ok) throw new Error(`Failed to load configurations for ${env}`);

        const data: ParameterDetail = await response.json();
        return data.configurations.map((config) => ({ ...config, environment: env }));
      }));

      const merged = detailResponses
        .flat()
        .sort((a, b) => {
          const envA = a.environment || '';
          const envB = b.environment || '';
          if (envA === envB) return a.projectName.localeCompare(b.projectName);
          return envA.localeCompare(envB);
        });

      setSelectedEnvConfigurations(merged);
    } catch (error) {
      setMessage({ type: 'error', text: `Error loading selected environments: ${error}` });
      setSelectedEnvConfigurations([]);
    } finally {
      setIsSelectedEnvConfigsLoading(false);
    }
  };

  const refreshSelectedParameterData = async () => {
    if (!selectedParameter) return;
    await handleParameterSelect(selectedParameter);
    await loadSelectedEnvironmentConfigurations(selectedParameter.configKey);
    await loadParameters();
  };

  const startRowEdit = (configId: number, currentValue: string) => {
    setEditingConfigId(configId);
    setRowEditValue(currentValue || '');
  };

  const cancelRowEdit = () => {
    setEditingConfigId(null);
    setRowEditValue('');
  };

  const saveRowEdit = async (configId: number) => {
    setRowActionConfigId(configId);
    try {
      const response = await fetch(`${API_BASE}/goanywhere/configs/${configId}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ configValue: rowEditValue })
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to update project-level value');
      }

      setMessage({ type: 'success', text: '✓ Project-level value updated successfully' });
      cancelRowEdit();
      await refreshSelectedParameterData();
    } catch (error) {
      setMessage({ type: 'error', text: `Error: ${error}` });
    } finally {
      setRowActionConfigId(null);
    }
  };

  const deleteRowConfig = async (configId: number, projectName: string, environment: string) => {
    const shouldDelete = window.confirm(`Delete this parameter for ${projectName} in ${environment}?`);
    if (!shouldDelete) return;

    setRowActionConfigId(configId);
    try {
      const response = await fetch(`${API_BASE}/goanywhere/configs/${configId}`, {
        method: 'DELETE'
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to delete project-level value');
      }

      setMessage({ type: 'success', text: '✓ Project-level value deleted successfully' });
      await refreshSelectedParameterData();
    } catch (error) {
      setMessage({ type: 'error', text: `Error: ${error}` });
    } finally {
      setRowActionConfigId(null);
    }
  };

  const handleUpdateParameter = async () => {
    if (!selectedParameter || !editValue) {
      setMessage({ type: 'error', text: 'Please enter a value' });
      return;
    }

    if (selectedEnvironments.size === 0) {
      setMessage({ type: 'error', text: 'Please select at least one environment' });
      return;
    }

    setIsSaving(true);
    try {
      let response;
      
      if (bulkUpdateMode && selectedEnvironments.size > 1) {
        // Use bulk endpoint for multiple environments
        response = await fetch(
          `${API_BASE}/goanywhere/parameters/${selectedParameter.configKey}/bulk`,
          {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ 
              configValue: editValue,
              environments: Array.from(selectedEnvironments)
            })
          }
        );
      } else {
        // Use single environment endpoint
        const env = Array.from(selectedEnvironments)[0];
        response = await fetch(
          `${API_BASE}/goanywhere/parameters/${selectedParameter.configKey}/${env}`,
          {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ configValue: editValue })
          }
        );
      }

      if (response.ok) {
        const result = await response.json();
        setMessage({ 
          type: 'success', 
          text: `✓ ${result.message}` 
        });
        setJustUpdated(true);
        // Reload parameter details to show updated values
        if (selectedParameter) {
          await handleParameterSelect(selectedParameter);
        }
        loadParameters();
      } else {
        const error = await response.json();
        setMessage({ type: 'error', text: error.error || 'Failed to update parameter' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: `Error: ${error}` });
    } finally {
      setIsSaving(false);
    }
  };

  const toggleEnvironment = (env: string) => {
    const newSet = new Set(selectedEnvironments);
    if (newSet.has(env)) {
      newSet.delete(env);
    } else {
      newSet.add(env);
    }
    setSelectedEnvironments(newSet);
    setShowEnvPreview(false);
    setEnvironmentPreviews([]);
    if (newSet.size > 1) {
      setBulkUpdateMode(true);
    }
  };

  const selectAllEnvironments = () => {
    setSelectedEnvironments(new Set(environments));
    setShowEnvPreview(false);
    setEnvironmentPreviews([]);
    setBulkUpdateMode(true);
  };

  const deselectAllEnvironments = () => {
    setSelectedEnvironments(new Set());
    setShowEnvPreview(false);
    setEnvironmentPreviews([]);
    setSelectedEnvConfigurations([]);
  };

  useEffect(() => {
    if (!selectedParameter) {
      setSelectedEnvConfigurations([]);
      return;
    }

    loadSelectedEnvironmentConfigurations(selectedParameter.configKey);
  }, [selectedParameter, selectedEnvironments]);

  const handleRefreshAllProjects = async () => {
    setIsRefreshing(true);
    try {
      // Reload all environments and parameters
      await loadEnvironments();
      if (selectedParameter) {
        await handleParameterSelect(selectedParameter);
      }
      await loadParameters();
      setMessage({
        type: 'success',
        text: '✓ All projects updated with latest data'
      });
      setJustUpdated(false);
    } catch (error) {
      setMessage({
        type: 'error',
        text: `Error refreshing projects: ${error}`
      });
    } finally {
      setIsRefreshing(false);
    }
  };

  const filteredParameters = parameters.filter(p =>
    p.configKey.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const configurationsToDisplay = selectedEnvironments.size > 1
    ? selectedEnvConfigurations
    : (parameterDetails?.configurations ?? []);

  // Non-admins only see masked sensitive values, so block them from editing
  // sensitive parameters (they would otherwise overwrite the value with the mask).
  const sensitiveLocked = !!parameterDetails?.isSensitive && !getIsAdmin();

  return (
    <div className="parameters-page">
      <div className="parameters-container">
        {/* Left Panel - Parameter List */}
        <div className="parameters-list-panel">
          <div className="panel-header">
            <h2>Parameters</h2>
          </div>

          {/* Multi-Environment Selector */}
          <div className="env-selector-panel">
            <div className="env-selector-header env-view-header">
              <label className="mode-label">View Environment:</label>
              <div className="env-view-buttons">
                {environments.map((env) => (
                  <button
                    key={env}
                    className={`env-btn ${selectedEnvironment === env ? 'active' : ''}`}
                    onClick={() => handleSelectEnvironment(env)}
                  >
                    {env}
                  </button>
                ))}
              </div>
            </div>

            <div className="env-selector-header">
              <label className="mode-label">Update Mode:</label>
              <button 
                className="btn-toggle-mode"
                onClick={() => setBulkUpdateMode(!bulkUpdateMode)}
              >
                {bulkUpdateMode ? '📋 Multi-Env' : '🔄 Single-Env'}
              </button>
            </div>
            
            {bulkUpdateMode && (
              <div className="env-checkboxes">
                <div className="env-checkbox-controls">
                  <button 
                    className="btn-env-control"
                    onClick={selectAllEnvironments}
                  >
                    ✓ All
                  </button>
                  <button 
                    className="btn-env-control"
                    onClick={deselectAllEnvironments}
                  >
                    ✗ Clear
                  </button>
                </div>
                <div className="env-checkbox-list">
                  {environments.map(env => (
                    <label key={env} className="env-checkbox">
                      <input
                        type="checkbox"
                        checked={selectedEnvironments.has(env)}
                        onChange={() => toggleEnvironment(env)}
                      />
                      <span className={selectedEnvironments.has(env) ? 'checked' : ''}>
                        {env}
                      </span>
                    </label>
                  ))}
                </div>
              </div>
            )}
          </div>

          <div className="search-box">
            <input
              type="text"
              placeholder="Search parameters..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="search-input"
            />
          </div>

          <div className="parameters-list">
            {!selectedEnvironment ? (
              <div className="empty">Select an environment to load parameters</div>
            ) : loading ? (
              <div className="loading">Loading parameters...</div>
            ) : filteredParameters.length === 0 ? (
              <div className="empty">No parameters found</div>
            ) : (
              filteredParameters.map((param) => (
                <div
                  key={`${param.configKey}-${param.environment}`}
                  className={`parameter-item ${selectedParameter?.configKey === param.configKey ? 'active' : ''}`}
                  onClick={() => handleParameterSelect(param)}
                >
                  <div className="param-name">{param.configKey}</div>
                  <div className="param-meta">
                    <span className="project-count">{param.projectCount} project(s)</span>
                    {param.isSensitive && <span className="badge-sensitive">Sensitive</span>}
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Right Panel - Parameter Details */}
        <div className="parameters-detail-panel">
          {parameterDetails ? (
            <div className="detail-content">
              <div className="detail-header">
                <h3>{parameterDetails.configKey}</h3>
                <span className="env-badge">{parameterDetails.environment}</span>
              </div>

              <div className="detail-info">
                <div className="info-row">
                  <label>Affected Projects:</label>
                  <span>{parameterDetails.projectCount}</span>
                </div>
                {parameterDetails.description && (
                  <div className="info-row">
                    <label>Description:</label>
                    <span>{parameterDetails.description}</span>
                  </div>
                )}
                <div className="info-row">
                  <label>Sensitive:</label>
                  <span>{parameterDetails.isSensitive ? '✓ Yes' : '✗ No'}</span>
                </div>
                <div className="info-row">
                  <label>Required:</label>
                  <span>{parameterDetails.isRequired ? '✓ Yes' : '✗ No'}</span>
                </div>
              </div>

              <div className="update-section">
                <label>Update Value {bulkUpdateMode && selectedEnvironments.size > 1 ? `for ${selectedEnvironments.size} Environments` : 'for Selected Environment'}:</label>
                <div className="env-badges">
                  {Array.from(selectedEnvironments).sort().map(env => (
                    <span key={env} className="badge-env-selected">
                      {env}
                    </span>
                  ))}
                </div>
                <div className="preview-action-row">
                  <button
                    onClick={handleShowEnvironmentPreview}
                    disabled={isPreviewLoading || selectedEnvironments.size === 0}
                    className="btn-preview-current"
                  >
                    {isPreviewLoading ? 'Loading Current Values...' : 'Show Current Values by Environment'}
                  </button>
                </div>

                {showEnvPreview && environmentPreviews.length > 0 && (
                  <div className="env-preview-panel">
                    <div className="env-preview-header">Current Values Before Update</div>
                    <div className="env-preview-table">
                      <div className="env-preview-row env-preview-row-head">
                        <div>Environment</div>
                        <div>Projects</div>
                        <div>Current Value</div>
                      </div>
                      {environmentPreviews.map((preview) => (
                        <div key={preview.environment} className="env-preview-row">
                          <div>{preview.environment}</div>
                          <div>{preview.projectCount}</div>
                          <div>
                            {preview.valueBreakdown.length > 1 ? (
                              <div className="env-value-breakdown">
                                {preview.valueBreakdown.map((entry) => (
                                  <div key={`${preview.environment}-${entry.value}`} className="env-value-item">
                                    <span className="env-value-text">{entry.value}</span>
                                    <span className="env-value-count">({entry.count})</span>
                                  </div>
                                ))}
                              </div>
                            ) : (
                              preview.currentValue
                            )}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                <div className="edit-group">
                  <input
                    type='text'
                    value={sensitiveLocked ? '' : editValue}
                    onChange={(e) => setEditValue(e.target.value)}
                    placeholder={sensitiveLocked ? 'Admin access required to edit' : 'Enter new value'}
                    className="edit-input"
                    disabled={sensitiveLocked}
                  />
                  <button
                    onClick={handleUpdateParameter}
                    disabled={isSaving || sensitiveLocked}
                    className="btn-update"
                  >
                    {isSaving ? 'Updating...' : `Update ${bulkUpdateMode && selectedEnvironments.size > 1 ? `All (${selectedEnvironments.size})` : 'Selected'}`}
                  </button>
                </div>
                {sensitiveLocked && (
                  <div className="message message-error">
                    This is a sensitive value. Only admin users can view or edit it.
                  </div>
                )}
                {message && (
                  <div className={`message message-${message.type}`}>
                    {message.text}
                    {justUpdated && message.type === 'success' && (
                      <button
                        onClick={handleRefreshAllProjects}
                        disabled={isRefreshing}
                        className="btn-refresh-all"
                      >
                        {isRefreshing ? 'Refreshing...' : '🔄 Refresh All Projects'}
                      </button>
                    )}
                  </div>
                )}
              </div>

              <div className="configurations-section">
                <h4>Current Values by Project (Selected Environments)</h4>
                <div className="configurations-table">
                  <div className="table-header">
                    <div className="col-project">Project</div>
                    <div className="col-env">Environment</div>
                    <div className="col-path">Project Path</div>
                    <div className="col-value">Current Value</div>
                    <div className="col-actions">Actions</div>
                  </div>
                  <div className="table-body">
                    {isSelectedEnvConfigsLoading ? (
                      <div className="empty">Loading selected environments...</div>
                    ) : configurationsToDisplay.length === 0 ? (
                      <div className="empty">No configurations found for selected environment(s)</div>
                    ) : configurationsToDisplay.map((config) => (
                      <div key={config.id} className="table-row">
                        <div className="col-project">{config.projectName}</div>
                        <div className="col-env">{config.environment || parameterDetails.environment}</div>
                        <div className="col-path">{config.projectPath}</div>
                        <div className="col-value">
                          {editingConfigId === config.id ? (
                            <input
                              type='text'
                              value={rowEditValue}
                              onChange={(e) => setRowEditValue(e.target.value)}
                              className="row-edit-input"
                            />
                          ) : (
                            (config.actualValue || '—')
                          )}
                        </div>
                        <div className="col-actions">
                          {editingConfigId === config.id ? (
                            <>
                              <button
                                className="btn-row-action btn-row-save"
                                onClick={() => saveRowEdit(config.id)}
                                disabled={rowActionConfigId === config.id}
                              >
                                {rowActionConfigId === config.id ? 'Saving...' : 'Save'}
                              </button>
                              <button
                                className="btn-row-action btn-row-cancel"
                                onClick={cancelRowEdit}
                                disabled={rowActionConfigId === config.id}
                              >
                                Cancel
                              </button>
                            </>
                          ) : (
                            <>
                              <button
                                className="btn-row-action"
                                onClick={() => startRowEdit(config.id, config.actualValue || '')}
                                disabled={rowActionConfigId === config.id || sensitiveLocked}
                                title={sensitiveLocked ? 'Admin access required' : undefined}
                              >
                                Edit
                              </button>
                              <button
                                className="btn-row-action btn-row-delete"
                                onClick={() => deleteRowConfig(config.id, config.projectName, config.environment || parameterDetails.environment)}
                                disabled={rowActionConfigId === config.id}
                              >
                                {rowActionConfigId === config.id ? 'Deleting...' : 'Delete'}
                              </button>
                            </>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </div>
          ) : (
            <div className="empty-detail">
              <div className="empty-icon">📋</div>
              <p>Select a parameter to view and edit details</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
