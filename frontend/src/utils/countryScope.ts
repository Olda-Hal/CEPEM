/**
 * Country <-> country-scope-id mapping shared across pages
 * (mirror of DatabaseAPI CountryScopeMapper — keep in sync).
 */
export const countryScopeByCode: Record<string, number> = {
  CZ: 203,
  DE: 276,
  SK: 703,
  AT: 40,
  PL: 616,
  NL: 528
};

export const countryCodeByScopeId: Record<number, string> = Object.fromEntries(
  Object.entries(countryScopeByCode).map(([code, scopeId]) => [scopeId, code])
);

export const countryOptions = Object.keys(countryScopeByCode);

export const normalizeCountryCode = (countryCode?: string): string => {
  if (!countryCode) {
    return 'CZ';
  }

  return countryCode.trim().toUpperCase().slice(0, 2);
};
