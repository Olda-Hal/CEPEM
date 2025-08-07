import { en, TranslationKeys } from './en';
import { cs } from './cs';

export type SupportedLanguages = 'en' | 'cs';

export const translations: Record<SupportedLanguages, TranslationKeys> = {
  en,
  cs
};

export const defaultLanguage: SupportedLanguages = 'cs';

export type { TranslationKeys };
export { en, cs };
