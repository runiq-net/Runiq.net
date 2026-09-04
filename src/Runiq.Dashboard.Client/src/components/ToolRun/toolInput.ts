import type { ToolJsonSchema } from '../../api/agentMetadataApi';

export type ToolInputValue = string | boolean;
export type ToolInputValues = Record<string, ToolInputValue>;

export function getSchemaProperties(schema?: ToolJsonSchema): [string, ToolJsonSchema][] {
  return schema?.properties ? Object.entries(schema.properties) : [];
}

export function isRequired(schema: ToolJsonSchema | undefined, name: string): boolean {
  return Boolean(schema?.required?.includes(name));
}

export function createDefaultInputValues(schema?: ToolJsonSchema): ToolInputValues {
  return Object.fromEntries(getSchemaProperties(schema).map(([name, property]) => [
    name,
    property.type === 'boolean' ? false : '',
  ]));
}

export function buildInputObject(schema: ToolJsonSchema, values: ToolInputValues): Record<string, unknown> {
  return Object.fromEntries(getSchemaProperties(schema).map(([name, property]) => {
    const value = values[name];
    if (property.type === 'integer') return [name, value === '' ? null : Number.parseInt(String(value), 10)];
    if (property.type === 'number') return [name, value === '' ? null : Number.parseFloat(String(value))];
    if (property.type === 'boolean') return [name, Boolean(value)];
    return [name, value ?? ''];
  }));
}

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
