import { useState, useEffect } from "react";
import { getSource } from "../api";
import "./GoAnywhereConfigPage.css";
import ProjectsTab from "./GoAnywhereProjectsTab";
import type {
  GAProject,
  GAConfig,
  AvailableParameter,
  Environment,
  NewProjectState,
  NewParameterState,
} from "./goanywhereTypes";

export default function GoAnywhereConfigPage() {
  const [projects, setProjects] = useState<GAProject[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<number | null>(null);
  const [selectedEnvironment, setSelectedEnvironment] = useState<Environment>(
    getSource() === "FCB" ? "FCB" : "DATO"
  );
  const [configs, setConfigs] = useState<GAConfig[]>([]);
  const [availableParameters, setAvailableParameters] = useState<AvailableParameter[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [configSearch, setConfigSearch] = useState("");
  const [projectSearch, setProjectSearch] = useState("");
  const [showNewProjectForm, setShowNewProjectForm] = useState(false);
  const [showAddParameterForm, setShowAddParameterForm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [newProject, setNewProject] = useState<NewProjectState>({ name: "", description: "" });
  const [newParameter, setNewParameter] = useState<NewParameterState>({
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

  // Load the master list of parameters for the selected environment
  useEffect(() => {
    loadAvailableParameters(selectedEnvironment);
  }, [selectedEnvironment]);

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

  const loadAvailableParameters = async (environment: string) => {
    try {
      const response = await fetch(
        `${API_BASE}/goanywhere/parameters?environment=${environment}`
      );
      if (!response.ok) {
        throw new Error(`Failed to load parameters: ${response.statusText}`);
      }
      const data: AvailableParameter[] = await response.json();
      setAvailableParameters(data);
    } catch (err) {
      console.error("Error loading available parameters:", err);
      setAvailableParameters([]);
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
      setNewParameter({
        configKey: "",
        configValue: "",
        description: "",
        isRequired: false,
        isSensitive: false,
      });
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
    ? projects.filter(
        (p) =>
          p.name.toLowerCase().includes(projectSearch.toLowerCase()) ||
          (p.description && p.description.toLowerCase().includes(projectSearch.toLowerCase()))
      )
    : projects;

  // Filter configs for search
  const filteredConfigs = configSearch.trim()
    ? configs.filter(
        (c) =>
          c.configKey.toLowerCase().includes(configSearch.toLowerCase()) ||
          (c.description && c.description.toLowerCase().includes(configSearch.toLowerCase()))
      )
    : configs;

  return (
    <div className="ga-config-container">
      <h2>🔄 GoAnywhere Configuration Management</h2>
      <p className="subtitle">
        Manage GoAnywhere projects and their configuration parameters.
      </p>

      {error && <div className="error-message">{error}</div>}

      <ProjectsTab
        loading={loading}
        saving={saving}
        projects={projects}
        filteredProjects={filteredProjects}
        projectSearch={projectSearch}
        setProjectSearch={setProjectSearch}
        selectedProjectId={selectedProjectId}
        setSelectedProjectId={setSelectedProjectId}
        selectedProject={selectedProject}
        selectedEnvironment={selectedEnvironment}
        setSelectedEnvironment={setSelectedEnvironment}
        configs={configs}
        filteredConfigs={filteredConfigs}
        configSearch={configSearch}
        setConfigSearch={setConfigSearch}
        getEnvironmentSpecificPath={getEnvironmentSpecificPath}
        showNewProjectForm={showNewProjectForm}
        setShowNewProjectForm={setShowNewProjectForm}
        newProject={newProject}
        setNewProject={setNewProject}
        handleCreateProject={handleCreateProject}
        showAddParameterForm={showAddParameterForm}
        setShowAddParameterForm={setShowAddParameterForm}
        newParameter={newParameter}
        setNewParameter={setNewParameter}
        handleAddParameter={handleAddParameter}
        availableParameters={availableParameters}
      />
    </div>
  );
}
