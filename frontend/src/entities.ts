// Entity metadata that drives the generic list/edit screens.
// This mirrors the original generic "work-with" engine (UTCOM_WW) that the RPG
// programs UT2010-UT2018 shared.

export type FieldType = "text" | "number" | "bool" | "textarea" | "lookup";

export interface FieldDef {
  name: string;
  label: string;
  type: FieldType;
  /** for lookup fields: the route of the referenced entity */
  lookup?: string;
  /** show in the list grid */
  inList?: boolean;
}

export interface EntityDef {
  route: string;
  title: string;
  /** original RPG program for traceability */
  legacy: string;
  fields: FieldDef[];
}

export const entities: Record<string, EntityDef> = {
  "variable-types": {
    route: "variable-types",
    title: "Variable Types",
    legacy: "UT2010 / UTCFGVTP",
    fields: [
      { name: "name", label: "Name", type: "text", inList: true },
      { name: "description", label: "Description", type: "text", inList: true },
      { name: "validationProcedure", label: "Validation Procedure", type: "text" },
      { name: "information", label: "Information", type: "textarea" },
    ],
  },
  "resolution-methods": {
    route: "resolution-methods",
    title: "Scope Resolution Methods",
    legacy: "UT2011 / UTCFGSRM",
    fields: [
      { name: "name", label: "Name", type: "text", inList: true },
      { name: "description", label: "Description", type: "text", inList: true },
      { name: "information", label: "Information", type: "textarea" },
    ],
  },
  servers: {
    route: "servers",
    title: "Servers",
    legacy: "UT2012 / UTCFGSRV",
    fields: [
      { name: "name", label: "Name", type: "text", inList: true },
      { name: "description", label: "Description", type: "text", inList: true },
      { name: "matchValue", label: "Match Value", type: "text", inList: true },
      { name: "information", label: "Information", type: "textarea" },
    ],
  },
  contexts: {
    route: "contexts",
    title: "Contexts",
    legacy: "UT2014 / UTCFGCTX",
    fields: [
      { name: "name", label: "Name", type: "text", inList: true },
      { name: "description", label: "Description", type: "text", inList: true },
      { name: "isServer", label: "Is Server", type: "bool", inList: true },
      { name: "information", label: "Information", type: "textarea" },
    ],
  },
  extents: {
    route: "extents",
    title: "Extents",
    legacy: "UTCFGXTN",
    fields: [
      { name: "contextId", label: "Context", type: "lookup", lookup: "contexts", inList: true },
      { name: "name", label: "Name", type: "text", inList: true },
      { name: "description", label: "Description", type: "text", inList: true },
      { name: "information", label: "Information", type: "textarea" },
    ],
  },
  scopes: {
    route: "scopes",
    title: "Scopes",
    legacy: "UTCFGSCP",
    fields: [
      { name: "serverId", label: "Server", type: "lookup", lookup: "servers", inList: true },
      { name: "scopeResolutionMethodId", label: "Method", type: "lookup", lookup: "resolution-methods", inList: true },
      { name: "name", label: "Name", type: "text", inList: true },
      { name: "description", label: "Description", type: "text", inList: true },
      { name: "isServer", label: "Is Server", type: "bool" },
      { name: "matchValue", label: "Match Value", type: "text", inList: true },
      { name: "information", label: "Information", type: "textarea" },
    ],
  },
  "variable-definitions": {
    route: "variable-definitions",
    title: "Variable Definitions",
    legacy: "UT2018 / UTCFGVDF",
    fields: [
      { name: "extentId", label: "Extent", type: "lookup", lookup: "extents", inList: true },
      { name: "variableTypeId", label: "Type", type: "lookup", lookup: "variable-types", inList: true },
      { name: "name", label: "Name", type: "text", inList: true },
      { name: "description", label: "Description", type: "text", inList: true },
      { name: "valuesAreRestricted", label: "Restricted", type: "bool", inList: true },
      { name: "information", label: "Information", type: "textarea" },
    ],
  },
  "valid-values": {
    route: "valid-values",
    title: "Valid Values",
    legacy: "UTCFGVVL",
    fields: [
      { name: "variableDefinitionId", label: "Variable", type: "lookup", lookup: "variable-definitions", inList: true },
      { name: "value", label: "Value", type: "text", inList: true },
      { name: "description", label: "Description", type: "text", inList: true },
      { name: "information", label: "Information", type: "textarea" },
    ],
  },
  "variable-values": {
    route: "variable-values",
    title: "Variable Values",
    legacy: "UT2052 / UTCFGVAL",
    fields: [
      { name: "variableDefinitionId", label: "Variable", type: "lookup", lookup: "variable-definitions", inList: true },
      { name: "scopeId", label: "Scope", type: "lookup", lookup: "scopes", inList: true },
      { name: "value", label: "Value", type: "text", inList: true },
    ],
  },
};

export const menuItems = Object.values(entities);
