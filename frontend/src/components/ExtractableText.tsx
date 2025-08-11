import React from 'react';
import { useTranslation } from 'react-i18next';

interface ExtractableTextProps {
  /**
   * Klíč pro lokalizaci
   */
  i18nKey: string;
  /**
   * Výchozí text, který se zobrazí pokud není překlad nalezen
   * Také se použije pro automatickou extrakci do lokalizačních souborů
   */
  defaultText: string;
  /**
   * Parametry pro interpolaci
   */
  values?: Record<string, string | number>;
  /**
   * HTML tag, který se má použít pro renderování
   */
  as?: keyof React.JSX.IntrinsicElements;
  /**
   * CSS třídy
   */
  className?: string;
  /**
   * Další props pro element
   */
  [key: string]: any;
}

/**
 * Komponenta pro automatickou extrakci textů do lokalizačních souborů
 * 
 * Použití:
 * <ExtractableText 
 *   i18nKey="dashboard.welcome" 
 *   defaultText={t('common.welcome')} 
 *   values={{ name: t('common.defaultName') }}
 *   as="h1"
 *   className="title"
 * />
 */
export const ExtractableText: React.FC<ExtractableTextProps> = ({
  i18nKey,
  defaultText,
  values,
  as: Component = 'span',
  className,
  ...props
}) => {
  const { t } = useTranslation();
  
  const translatedText = t(i18nKey, { defaultValue: defaultText, ...values });
  
  return React.createElement(Component as any, { className, ...props }, translatedText);
};
