import { useState, useEffect } from 'react';
import '../pages/ParametersPage.css';

const API_BASE = 'http://localhost:5000/api';

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
    projectPath: string;
    configValue: string;
    actualValue: string;
    isSensitive: boolean;
  }>;
}

export default function ParametersPage() {
  const [parameters, setParameters] = useState<Parameter[]>([]);
  const [selectedEnvironment, setSelectedEnvironment] = useState<string>('DATO');
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

  // Load available environments
  useEffect(() => {
    loadEnvironments();
  }, []);

  // Load parameters when environment changes
  useEffect(() => {
    if (selectedEnvironment) {
      loadParameters();
    }
  }, [selectedEnvironment]);

  // Initialize selectedEnvironments when environments load
  useEffect(() => {
    if (environments.length > 0 && selectedEnvironments.size === 0) {
      setSelectedEnvironments(new Set([selectedEnvironment]));
    }
  }, [environments]);

  const loadEnvironments = async () => {
    try {
      const response = await fetch(`${API_BASE}/goanywhere/parameters`);
      if (response.ok) {
        const data: Parameter[] = await response.json();
        const envs = [...new Set(data.map((p: Parameter) => p.environment))].sort() as string[];
        setEnvironments(envs);
        if (envs.length > 0 && !selectedEnvironment) {
          setSelectedEnvironment(envs[0]);
        }
      }
    } catch (error) {
      console.error('Error loading environments:', error);
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
    if (newSet.size > 1) {
      setBulkUpdateMode(true);
    }
  };

  const selectAllEnvironments = () => {
    setSelectedEnvironments(new Set(environments));
    setBulkUpdateMode(true);
  };

  const deselectAllEnvironments = () => {
    setSelectedEnvironments(new Set());
  };

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
            {loading ? (
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
                <div className="edit-group">
                  <input
                    type={parameterDetails.isSensitive ? 'password' : 'text'}
                    value={editValue}
                    onChange={(e) => setEditValue(e.target.value)}
                    placeholder="Enter new value"
                    className="edit-input"
                  />
                  <button
                    onClick={handleUpdateParameter}
                    disabled={isSaving}
                    className="btn-update"
                  >
                    {isSaving ? 'Updating...' : `Update ${bulkUpdateMode && selectedEnvironments.size > 1 ? `All (${selectedEnvironments.size})` : 'Selected'}`}
                  </button>
                </div>
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
                <h4>Current Values by Project</h4>
                <div className="configurations-table">
                  <div className="table-header">
                    <div className="col-project">Project</div>
                    <div className="col-path">Project Path</div>
                    <div className="col-value">Current Value</div>
                  </div>
                  <div className="table-body">
                    {parameterDetails.configurations.map((config) => (
                      <div key={config.id} className="table-row">
                        <div className="col-project">{config.projectName}</div>
                        <div className="col-path">{config.projectPath}</div>
                        <div className="col-value">
                          {config.isSensitive ? '●●●●●●●●' : (config.actualValue || '—')}
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
