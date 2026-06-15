import { readFileSync, writeFileSync, existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const rootDir = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const outputPath = join(rootDir, "client", "env.js");
const envPaths = [join(rootDir, ".env"), join(rootDir, "client", ".env")];
const clientKeys = [
  "POKEMON2_API_BASE",
];

const values = {};

for (const envPath of envPaths) {
  if (!existsSync(envPath)) continue;
  Object.assign(values, parseEnv(readFileSync(envPath, "utf8")));
}

const publicValues = {};
for (const key of clientKeys) {
  if (values[key] !== undefined) publicValues[key] = values[key];
}

writeFileSync(
  outputPath,
  `window.POKEMON2_ENV = ${JSON.stringify(publicValues, null, 2)};\n`,
  "utf8"
);

console.log(`Wrote ${outputPath}`);

function parseEnv(text) {
  const parsed = {};
  for (const rawLine of text.split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith("#")) continue;
    const separator = line.indexOf("=");
    if (separator <= 0) continue;

    const key = line.slice(0, separator).trim();
    let value = line.slice(separator + 1).trim();
    if (
      (value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'"))
    ) {
      value = value.slice(1, -1);
    }
    parsed[key] = value;
  }
  return parsed;
}
