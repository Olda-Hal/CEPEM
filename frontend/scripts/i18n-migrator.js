#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

/**
 * Analyzátor pro nalezení hardcoded stringů v kódu
 */

const STRINGS_TO_EXTRACT = [
  /"([^"]+)"/g,  // Double quoted strings
  /'([^']+)'/g,  // Single quoted strings
  /`([^`]+)`/g,  // Template literals
];

const IGNORE_PATTERNS = [
  /^[a-z]+$/,  // Jednoslovné technické termíny
  /^\d+$/,     // Pouze číslice
  /^[A-Z_]+$/, // Konstanty
  /^[a-z]+\.[a-z]+$/,  // Už existující i18n klíče
  /className|style|src|href|alt|id|key|ref/,  // React props
];

// User-facing stringy vs technické stringy
const NON_USER_FACING_PATTERNS = [
  // Import cesty
  /^\.\/.*$/, /^\.\.\//,  // Relativní cesty
  /^[a-z-]+\/[a-z-]+$/,  // Package names
  /^@[a-z-]+\/[a-z-]+$/,  // Scoped packages
  /react|redux|router|dom|client|css|scss|sass/i,
  
  // CSS třídy a selektory
  /^[a-z-]+-[a-z-]+$/,  // CSS BEM třídy (dashboard-container, login-form)
  /^[a-z]+[A-Z][a-z]+$/,  // camelCase CSS (loadingContainer)
  /^#[a-fA-F0-9]{3,6}$/,  // Hex barvy
  /^\d+px$|^\d+%$|^\d+em$|^\d+rem$/,  // CSS jednotky
  
  // API endpointy a URL
  /^\/api\//, /^http/, /^https/,  // API cesty
  /localhost/i, /\.com|\.org|\.cz/i,
  
  // Technické stringy
  /authToken|localStorage|sessionStorage/i,
  /Content-Type|application\/json|Bearer/i,
  /console\.|window\.|document\./,
  
  // Coding patterns
  /^const \w+|^let \w+|^var \w+/,  // Deklarace proměnných
  /^\w+\(\)|^\w+\.\w+\(\)/,  // Volání funkcí
  /^}\)/,  // Template literal endings
  
  // File extensions a paths
  /\.(js|jsx|ts|tsx|css|scss|json)$/,
  /node_modules/,
  
  // Locale codes a konfigurace
  /^[a-z]{2}-[A-Z]{2}$/,  // cs-CZ, en-US
  /^[a-z]{2}$/,  // cs, en (když je kratší než 3 znaky)
  
  // Flagy a emoji
  /^🇨🇿$|^🇺🇸$/,
  
  // Debugging a development
  /^web-vitals$|^react-scripts$/,
  /testing-library/i,
  
  // Velmi technické stringy
  /HTTP error|status:|Bearer \$|this\.baseUrl/i,
];

class I18nAnalyzer {
  constructor(srcDir = 'src') {
    this.srcDir = srcDir;
  }

  findFiles() {
    const files = [];
    
    const scanDir = (dir) => {
      const entries = fs.readdirSync(dir);
      
      for (const entry of entries) {
        const fullPath = path.join(dir, entry);
        const stat = fs.statSync(fullPath);
        
        if (stat.isDirectory() && !entry.startsWith('.') && entry !== 'node_modules') {
          scanDir(fullPath);
        } else if (stat.isFile() && /\.(ts|tsx|js|jsx)$/.test(entry)) {
          files.push(fullPath);
        }
      }
    };
    
    scanDir(this.srcDir);
    return files;
  }

  analyzeFile(filePath) {
    const content = fs.readFileSync(filePath, 'utf8');
    const strings = new Set();
    
    for (const pattern of STRINGS_TO_EXTRACT) {
      let match;
      while ((match = pattern.exec(content)) !== null) {
        const str = match[1];
        
        if (this.shouldIgnore(str)) continue;
        if (str.length < 3 || str.length > 100) continue;
        
        strings.add(str);
      }
    }
    
    return Array.from(strings);
  }

  shouldIgnore(str) {
    // Základní ignore patterns
    if (IGNORE_PATTERNS.some(pattern => pattern.test(str))) {
      return true;
    }
    
    // Filtrování neuser-facing stringů
    if (NON_USER_FACING_PATTERNS.some(pattern => pattern.test(str))) {
      return true;
    }
    
    // Velmi krátké stringy (jen písmena/číslice)
    if (str.length <= 3 && /^[a-zA-Z0-9]+$/.test(str)) {
      return true;
    }
    
    // Stringy které obsahují pouze speciální znaky
    if (/^[^a-zA-ZÀ-ÿ\u0100-\u017F\u0400-\u04FF]+$/.test(str)) {
      return true;
    }
    
    return false;
  }

  generateKey(str) {
    let key = str.toLowerCase()
      .replace(/[^a-z0-9\s]/g, '')
      .replace(/\s+/g, ' ')
      .trim()
      .replace(/\s/g, '_');
    
    if (key.length > 30) {
      key = key.substring(0, 30);
    }
    
    return key;
  }

  analyze() {
    const files = this.findFiles();
    const report = {
      totalFiles: files.length,
      filesWithStrings: 0,
      totalStrings: 0,
      stringsByFile: {}
    };

    for (const file of files) {
      const strings = this.analyzeFile(file);
      
      if (strings.length > 0) {
        report.filesWithStrings++;
        report.totalStrings += strings.length;
        report.stringsByFile[file] = strings.map(str => ({
          string: str,
          suggestedKey: this.generateKey(str)
        }));
      }
    }

    return report;
  }

  generateReport() {
    const analysis = this.analyze();
    
    console.log('\n=== I18N ANALYSIS REPORT (User-Facing Strings Only) ===\n');
    console.log(`Total files analyzed: ${analysis.totalFiles}`);
    console.log(`Files with user-facing strings: ${analysis.filesWithStrings}`);
    console.log(`User-facing strings found: ${analysis.totalStrings}\n`);
    
    if (analysis.totalStrings === 0) {
      console.log('✅ No user-facing hardcoded strings found!');
      console.log('🔧 All user-visible texts are properly localized.');
      return;
    }
    
    console.log('📋 User-facing strings that need localization:\n');
    
    for (const [file, strings] of Object.entries(analysis.stringsByFile)) {
      console.log(`📁 ${file} (${strings.length} strings):`);
      
      strings.forEach((item, index) => {
        console.log(`  ${index + 1}. "${item.string}"`);
        console.log(`     → Suggested key: ${item.suggestedKey}`);
      });
      
      console.log('');
    }
    
    console.log('\n🔧 Next steps:');
    console.log('1. Review the suggested keys above');
    console.log('2. Add them to your translation files (en.json, cs.json)');
    console.log('3. Replace hardcoded strings with t() calls');
    console.log('4. Run npm run i18n:extract to scan for new t() calls');
    console.log('\n💡 Note: Technical strings (imports, CSS classes, API endpoints) are filtered out.');
  }
}

if (require.main === module) {
  const analyzer = new I18nAnalyzer();
  analyzer.generateReport();
}

module.exports = I18nAnalyzer;
