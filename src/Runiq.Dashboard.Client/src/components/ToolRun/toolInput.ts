import type { ToolJsonSchema } from '../../api/agentMetadataApi';

/** Represents a value accepted by the dashboard's generated tool form. */
export type ToolInputValue = string | boolean;
/** Maps tool input property names to their current form values. */
export type ToolInputValues = Record<string, ToolInputValue>;

/** Returns the declared properties of a tool input schema. */
export function getSchemaProperties(schema?: ToolJsonSchema): [string, ToolJsonSchema][] {
  return schema?.properties ? Object.entries(schema.properties) : [];
}

/** Determines whether a named tool input property is required. */
export function isRequired(schema: ToolJsonSchema | undefined, name: string): boolean {
  return Boolean(schema?.required?.includes(name));
}

/** Creates the initial form values for a tool input schema. */
export function createDefaultInputValues(schema?: ToolJsonSchema): ToolInputValues {
  return Object.fromEntries(getSchemaProperties(schema).map(([name, property]) => [
    name,
    property.type === 'boolean' ? false : '',
  ]));
}

/** Converts form values into the typed object sent to a tool endpoint. */
export function buildInputObject(schema: ToolJsonSchema, values: ToolInputValues): Record<string, unknown> {
  return Object.fromEntries(getSchemaProperties(schema).map(([name, property]) => {
    const value = values[name];
    if (property.type === 'integer') return [name, value === '' ? null : Number.parseInt(String(value), 10)];
    if (property.type === 'number') return [name, value === '' ? null : Number.parseFloat(String(value))];
    if (property.type === 'boolean') return [name, Boolean(value)];
    return [name, value ?? ''];
  }));
}

/** Validates required and numeric tool inputs before execution. */
export function validateInputValues(schema: ToolJsonSchema, values: ToolInputValues): Record<string, string> {
  const errors: Record<string, string> = {};
  for (const [name, property] of getSchemaProperties(schema)) {
    if (!isRequired(schema, name) || property.type === 'boolean') continue;
    const value = values[name];
    if (value === undefined || String(value).trim() === '') errors[name] = 'Required field.';
    else if ((property.type === 'integer' || property.type === 'number') && Number.isNaN(Number(value))) errors[name] = 'Enter a valid number.';
  }
  return errors;
}
