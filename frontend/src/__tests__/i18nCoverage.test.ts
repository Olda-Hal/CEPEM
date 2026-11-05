import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';
import { describe, expect, it } from 'vitest';

/**
 * Guards the frontend against regressions from the earlier hardcoded-Czech
 * state:
 *  - every literal translation key used through t(...) must exist in cs/en/nl
 *  - user-visible JSX text and translatable attributes must not be hardcoded
 *
 * Technical tokens that are intentionally not translated live in
 * ALLOWED_LITERALS below. Add to that list only for language-neutral values
 * (brand names, units, keyboard hints, identifiers), never for real UI copy.
 */

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SRC = path.resolve(HERE, '..');
const LOCALES = path.join(SRC, 'locales');
const LANGUAGES = ['cs', 'en', 'nl'] as const;

const IGNORED_PATHS = [
  /\/__tests__\//,
  /\.test\.(ts|tsx)$/,
  /\.spec\.(ts|tsx)$/,
  /\/locales\//,
  /\/i18n\//,
  /\/types\//,
  /setupTests/,
  /reportWebVitals/,
  /react-app-env/,
  /utils\/api\.ts$/
];

const TRANSLATABLE_ATTRIBUTES = new Set([
  'placeholder',
  'title',
  'aria-label',
  'aria-description',
  'alt'
]);

/** Language-neutral values that are deliberately not translated. */
const ALLOWED_LITERALS = new Set([
  'CEPEM',
  'Grafana',
  'UID',
  'UID:',
  'ID:',
  'CS',
  'EN',
  'NL',
  'Ctrl K',
  'Esc',
  'mm',
  'KB',
  'B',
  'i',
  '&times;'
]);

interface Finding {
  file: string;
  line: number;
  kind: string;
  value: string;
}

const RELATIVE = (file: string) => path.relative(SRC, file).split(path.sep).join('/');

function collectSourceFiles(dir: string): string[] {
  const files: string[] = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      files.push(...collectSourceFiles(fullPath));
    } else if (/\.(ts|tsx)$/.test(entry.name) && !IGNORED_PATHS.some((r) => r.test(fullPath))) {
      files.push(fullPath);
    }
  }
  return files;
}

function hasTranslatableLetters(value: string): boolean {
  // The app's copy is cs/en/nl, all Latin-script. Script matching avoids
  // treating non-letter symbols such as × or ℹ as translatable text.
  return /\p{Script=Latin}/u.test(value);
}

function isUserFacingLiteral(value: string): boolean {
  const trimmed = value.trim();
  return trimmed.length > 0 && hasTranslatableLetters(trimmed) && !ALLOWED_LITERALS.has(trimmed);
}

function literalText(node: ts.Node | undefined): string | null {
  if (!node) return null;
  return ts.isStringLiteral(node) || ts.isNoSubstitutionTemplateLiteral(node) ? node.text : null;
}

/** Literal strings that a JSX child expression can render as text. */
function renderedLiterals(expression: ts.Expression | undefined): string[] {
  if (!expression) return [];
  if (ts.isStringLiteral(expression) || ts.isNoSubstitutionTemplateLiteral(expression)) {
    return [expression.text];
  }
  if (ts.isConditionalExpression(expression)) {
    return [...renderedLiterals(expression.whenTrue), ...renderedLiterals(expression.whenFalse)];
  }
  if (ts.isParenthesizedExpression(expression)) {
    return renderedLiterals(expression.expression);
  }
  if (ts.isTemplateExpression(expression) && expression.templateSpans.length === 0) {
    return [expression.head.text];
  }
  return [];
}

function scanFiles(files: string[]) {
  const findings: Finding[] = [];
  const keys = new Set<string>();

  for (const file of files) {
    const source = fs.readFileSync(file, 'utf8');
    const sourceFile = ts.createSourceFile(
      file,
      source,
      ts.ScriptTarget.Latest,
      true,
      file.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.TS
    );
    const relative = RELATIVE(file);
    const lineOf = (node: ts.Node) =>
      sourceFile.getLineAndCharacterOfPosition(node.getStart()).line + 1;
    const report = (node: ts.Node, kind: string, value: string) => {
      findings.push({
        file: relative,
        line: lineOf(node),
        kind,
        value: value.replace(/\s+/g, ' ')
      });
    };

    const visit = (node: ts.Node) => {
      if (ts.isJsxText(node)) {
        const value = node.text.trim();
        if (isUserFacingLiteral(value)) report(node, 'jsx-text', value);
      }

      // Only direct JSX children render text; the same expression node type is
      // also used for attributes (className={...}), where literals are not UI.
      if (ts.isJsxExpression(node) && node.expression && !ts.isJsxAttribute(node.parent)) {
        for (const value of renderedLiterals(node.expression)) {
          if (isUserFacingLiteral(value)) report(node, 'jsx-expression', value);
        }
      }

      if (ts.isJsxAttribute(node) && TRANSLATABLE_ATTRIBUTES.has(node.name.getText())) {
        const initializer = node.initializer;
        let value: string | null = null;
        if (initializer && ts.isStringLiteral(initializer)) {
          value = initializer.text;
        } else if (initializer && ts.isJsxExpression(initializer)) {
          value = literalText(initializer.expression);
        }
        if (value && isUserFacingLiteral(value)) {
          report(node, `attribute:${node.name.getText()}`, value);
        }
      }

      if (ts.isCallExpression(node)) {
        const callee = node.expression;
        const isTranslationCall =
          (ts.isIdentifier(callee) && callee.text === 't') ||
          (ts.isPropertyAccessExpression(callee) && callee.name.text === 't');
        if (isTranslationCall) {
          const key = literalText(node.arguments[0]);
          if (key) keys.add(key);
        }

        const calleeName = ts.isIdentifier(callee)
          ? callee.text
          : ts.isPropertyAccessExpression(callee)
            ? callee.name.text
            : '';
        const isToast =
          ts.isPropertyAccessExpression(callee) &&
          ts.isIdentifier(callee.expression) &&
          callee.expression.text === 'toast';
        if (['alert', 'confirm', 'setError'].includes(calleeName) || isToast) {
          const value = literalText(node.arguments[0]);
          if (value && isUserFacingLiteral(value)) {
            report(node, isToast ? `toast.${calleeName}` : calleeName, value);
          }
        }
      }

      ts.forEachChild(node, visit);
    };

    visit(sourceFile);
  }

  return { findings, keys };
}

function loadLocale(language: string): Record<string, unknown> {
  return JSON.parse(fs.readFileSync(path.join(LOCALES, `${language}.json`), 'utf8'));
}

function lookup(locale: Record<string, unknown>, key: string): string | null {
  let current: unknown = locale;
  for (const segment of key.split('.')) {
    if (!current || typeof current !== 'object' || !(segment in current)) return null;
    current = (current as Record<string, unknown>)[segment];
  }
  return typeof current === 'string' && current.length > 0 ? current : null;
}

describe('frontend i18n coverage', () => {
  const files = collectSourceFiles(SRC);
  const { findings, keys } = scanFiles(files);
  const locales = Object.fromEntries(LANGUAGES.map((lng) => [lng, loadLocale(lng)]));

  it('has no hardcoded user-facing strings', () => {
    const message = findings
      .map((finding) => `${finding.file}:${finding.line} [${finding.kind}] ${finding.value}`)
      .join('\n');
    expect(findings, `Hardcoded user-facing strings found:\n${message}`).toEqual([]);
  });

  it('defines every literal translation key in cs/en/nl', () => {
    const problems: string[] = [];
    for (const language of LANGUAGES) {
      for (const key of [...keys].sort()) {
        if (lookup(locales[language]!, key) == null) {
          problems.push(`${language}: missing "${key}"`);
        }
      }
    }
    expect(problems, `Missing translation keys:\n${problems.join('\n')}`).toEqual([]);
  });
});
