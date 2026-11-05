#!/usr/bin/env node
/**
 * Guard: no hardcoded colour values outside the theme token file.
 *
 * Every UI colour must come from a CSS custom property defined in
 * src/themes/colors.css, so the light/dark switch can reach every screen.
 *
 * Allowed exceptions:
 *  - src/themes/colors.css   — the token definitions themselves
 *  - src/utils/tdbRender.ts  — the fixed MEIK medical colour table (device
 *                              imaging scale, intentionally independent of UI
 *                              theming)
 */
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const SRC = fileURLToPath(new URL('../src', import.meta.url));

const ALLOWED_FILES = new Set(['themes/colors.css', 'utils/tdbRender.ts']);

const COLOR_LITERAL = /#[0-9a-fA-F]{3,8}\b|\b(?:rgb|rgba|hsl|hsla)\s*\(/;
const COLOR_PROPERTY =
  /(?:^|[;{\s])(?:color|background(?:-color)?|border(?:-[a-z]+)?|fill|stroke|box-shadow|text-shadow|outline(?:-color)?)\s*[:=]/i;
const NAMED_COLOR =
  /\b(?:white|black|red|green|blue|gray|grey|yellow|orange|purple|silver|maroon|navy|teal|olive|lime|aqua|fuchsia)\b/i;

const SCAN_EXTENSIONS = new Set(['.css', '.ts', '.tsx']);

const findings = [];

function isExcluded(relativePath) {
  const normalized = relativePath.split(sep).join('/');
  if (ALLOWED_FILES.has(normalized)) return true;
  if (normalized.includes('/__tests__/') || normalized.startsWith('__tests__/')) return true;
  if (/\.(?:test|spec)\.(?:ts|tsx)$/.test(normalized)) return true;
  if (normalized.startsWith('types/')) return true;
  return false;
}

function walk(directory) {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const absolute = join(directory, entry.name);
    if (entry.isDirectory()) {
      walk(absolute);
      continue;
    }
    if (!SCAN_EXTENSIONS.has(entry.name.slice(entry.name.lastIndexOf('.')))) continue;

    const relativePath = relative(SRC, absolute).split(sep).join('/');
    if (isExcluded(relativePath)) continue;

    const lines = readFileSync(absolute, 'utf8').split(/\r?\n/);
    lines.forEach((line, index) => {
      if (COLOR_LITERAL.test(line)) {
        findings.push({ file: relativePath, line: index + 1, reason: 'hardcoded colour literal', text: line.trim() });
        return;
      }
      if (COLOR_PROPERTY.test(line) && NAMED_COLOR.test(line)) {
        findings.push({ file: relativePath, line: index + 1, reason: 'named colour', text: line.trim() });
      }
    });
  }
}

walk(SRC);

if (findings.length > 0) {
  console.error('Hardcoded colours found outside src/themes/colors.css:\n');
  for (const finding of findings) {
    console.error(`  ${finding.file}:${finding.line} [${finding.reason}]`);
    console.error(`    ${finding.text}`);
  }
  console.error(`\n${findings.length} finding(s). Use a theme token instead.`);
  process.exit(1);
}

console.log('No hardcoded colours found; every colour comes from a theme token.');
