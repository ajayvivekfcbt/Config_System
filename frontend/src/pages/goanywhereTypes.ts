export interface GAProject {
  id: number;
  name: string;
  description?: string;
  projectPath?: string;
  extentName?: string;
}

export interface GAConfig {
  id: number;
  configKey: string;
  configValue?: string;
  description?: string;
  isRequired: boolean;
  isSensitive: boolean;
}

export interface AvailableParameter {
  configKey: string;
  environment: string;
  projectCount: number;
  sampleValue?: string;
  isSensitive: boolean;
}

export type Environment = "DATO" | "DATI" | "DATU" | "DATV" | "DATN" | "FCB";

export interface NewProjectState {
  name: string;
  description: string;
}

export interface NewParameterState {
  configKey: string;
  configValue: string;
  description: string;
  isRequired: boolean;
  isSensitive: boolean;
}
