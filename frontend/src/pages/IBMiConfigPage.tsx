import { useState, useEffect } from 'react';
import { getIsAdmin } from '../api';
import './IBMiConfigPage.css';

const API_BASE = '/api';
const SYSTEM = 'IBMI';

interface IBMiProject {
  id: number;
  projectName: string;
  projectPath: string;
  configCount: number;
}

interface IBMiConfig {
  id: number;
  projectId: number;
  projectName: string;
  configKey: string;
  environment: string;
  configValue: string;
  isSensitive: boolean;
  isRequired: boolean;
}

export default function IBMiConfigPage() {
  const [projects, setProjects] = useState<IBMiProject[]>([]);
  const [selectedProject, setSelectedProject] = useState<IBMiProject | null>(null);
  const [configurations, setConfigurations] = useState<IBMiConfig[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  useEffect(() => {
    loadProjects();
  }, []);

  const loadProjects = async () => {
    setLoading(true);
    try {
      const response = await fetch(`${API_BASE}/ibmi/projects?system=${SYSTEM}`);
      if (response.ok) {
        const data = await response.json();
        setProjects(data);
      } else {
        setMessage({ type: 'error', text: 'Failed to load IBMi projects' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: `Error: ${error}` });
    } finally {
      setLoading(false);
    }
  };

  const loadConfigurations = async (projectId: number) => {
    setLoading(true);
    try {
      const response = await fetch(
        `${API_BASE}/ibmi/configs?system=${SYSTEM}&projectId=${projectId}`
      );
      if (response.ok) {
        const data = await response.json();
        setConfigurations(data);
      } else {
        setMessage({ type: 'error', text: 'Failed to load configurations' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: `Error: ${error}` });
    } finally {
      setLoading(false);
    }
  };

  const handleProjectSelect = (project: IBMiProject) => {
    setSelectedProject(project);
    loadConfigurations(project.id);
  };

  const filteredProjects = projects.filter(p =>
    p.projectName.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className="ibmi-config-page">
      <div className="ibmi-container">
        {/* Left Panel - Project List */}
        <div className="ibmi-list-panel">
          <div className="panel-header">
            <h2>🖥️ IBMi Projects</h2>
            <span className="project-count">{projects.length}</span>
          </div>

          <div className="search-box">
            <input
              type="text"
              placeholder="Search projects..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="search-input"
            />
          </div>

          <div className="projects-list">
            {loading ? (
              <div className="loading">Loading...</div>
            ) : filteredProjects.length === 0 ? (
              <div className="empty-state">No IBMi projects found</div>
            ) : (
              filteredProjects.map(project => (
                <div
                  key={project.id}
                  className={`project-item ${selectedProject?.id === project.id ? 'active' : ''}`}
                  onClick={() => handleProjectSelect(project)}
                >
                  <div className="project-name">{project.projectName}</div>
                  <div className="project-meta">
                    <span className="config-count">{project.configCount} config(s)</span>
                    <span className="project-path">{project.projectPath}</span>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Right Panel - Configuration Details */}
        <div className="ibmi-detail-panel">
          {selectedProject ? (
            <div className="detail-content">
              <div className="detail-header">
                <h3>{selectedProject.projectName}</h3>
                <span className="detail-badge">{selectedProject.projectPath}</span>
              </div>

              <div className="detail-info">
                <div className="info-row">
                  <label>Project ID:</label>
                  <span>{selectedProject.id}</span>
                </div>
                <div className="info-row">
                  <label>Total Configurations:</label>
                  <span>{configurations.length}</span>
                </div>
              </div>

              <div className="configurations-section">
                <h4>Configurations</h4>
                {configurations.length === 0 ? (
                  <div className="empty-configs">
                    No configurations for this project
                  </div>
                ) : (
                  <div className="configurations-table">
                    <table>
                      <thead>
                        <tr>
                          <th>Key</th>
                          <th>Environment</th>
                          <th>Value</th>
                          <th>Type</th>
                        </tr>
                      </thead>
                      <tbody>
                        {configurations.map(config => (
                          <tr key={config.id}>
                            <td className="config-key">{config.configKey}</td>
                            <td className="config-env">{config.environment}</td>
                            <td className="config-value">
                              {config.isSensitive && !getIsAdmin() ? '●●●●●●●●' : config.configValue}
                            </td>
                            <td className="config-type">
                              {config.isRequired ? (
                                <span className="badge-required">Required</span>
                              ) : (
                                <span className="badge-optional">Optional</span>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </div>
          ) : (
            <div className="empty-selection">
              <div className="empty-icon">📋</div>
              <p>Select a project to view configurations</p>
            </div>
          )}
        </div>
      </div>

      {message && (
        <div className={`message message-${message.type}`}>
          {message.text}
        </div>
      )}
    </div>
  );
}
