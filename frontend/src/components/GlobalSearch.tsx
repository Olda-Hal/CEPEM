import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { apiClient, ApiError } from '../utils/api';
import './GlobalSearch.css';

interface SearchPatient {
  id: number;
  uid: string;
  firstName: string;
  lastName: string;
  birthDate: string;
  email?: string | null;
  phoneNumber?: string | null;
}

interface SearchVisit {
  id: number;
  patientId: number;
  patientName: string;
  visitDate: string;
  note?: string | null;
  hasIntakeForm: boolean;
}

interface SearchDocument {
  documentId: number;
  patientId: number;
  patientName: string;
  fileName: string;
  uploadedAt: string;
  snippet?: string | null;
}

interface SearchEvaluation {
  caseId: number;
  title: string;
  side?: string | null;
  posture?: string | null;
  reviewCount: number;
  createdAtUtc: string;
}

interface SearchEmployee {
  id: number;
  fullName: string;
  email?: string | null;
  active: boolean;
  roles: string[];
}

interface GlobalSearchResponse {
  query: string;
  patients: SearchPatient[];
  visits: SearchVisit[];
  documents: SearchDocument[];
  evaluations: SearchEvaluation[];
  employees: SearchEmployee[];
}

type GroupKey = 'patients' | 'visits' | 'documents' | 'evaluations' | 'employees';

interface ResultItem {
  key: string;
  group: GroupKey;
  label: string;
  meta: string;
  onSelect: () => void;
}

const EMPTY_RESPONSE: GlobalSearchResponse = {
  query: '',
  patients: [],
  visits: [],
  documents: [],
  evaluations: [],
  employees: []
};

export const GlobalSearch: React.FC = () => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [response, setResponse] = useState<GlobalSearchResponse>(EMPTY_RESPONSE);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeIndex, setActiveIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const requestRef = useRef(0);

  const close = useCallback(() => {
    setOpen(false);
    setQuery('');
    setResponse(EMPTY_RESPONSE);
    setError(null);
    setActiveIndex(0);
  }, []);

  // Ctrl/Cmd+K opens the palette from anywhere inside the app shell.
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        setOpen(true);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  useEffect(() => {
    if (open) {
      const timer = window.setTimeout(() => inputRef.current?.focus(), 0);
      return () => window.clearTimeout(timer);
    }
    return undefined;
  }, [open]);

  useEffect(() => {
    const term = query.trim();
    if (!open || term.length < 2) {
      setResponse(EMPTY_RESPONSE);
      setLoading(false);
      setError(null);
      return undefined;
    }

    const requestId = ++requestRef.current;
    setLoading(true);
    setError(null);
    const timer = window.setTimeout(async () => {
      try {
        const result = await apiClient.get<GlobalSearchResponse>(
          `/api/search?q=${encodeURIComponent(term)}&limit=5`
        );
        if (requestRef.current === requestId) {
          setResponse(result);
          setActiveIndex(0);
        }
      } catch (err) {
        if (requestRef.current === requestId) {
          setResponse(EMPTY_RESPONSE);
          setError(err instanceof ApiError ? err.message : t('globalSearch.searchFailed'));
        }
      } finally {
        if (requestRef.current === requestId) {
          setLoading(false);
        }
      }
    }, 250);

    return () => window.clearTimeout(timer);
  }, [open, query, t]);

  const formatDate = useCallback(
    (value: string) =>
      new Date(value).toLocaleDateString(i18n.language, {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric'
      }),
    [i18n.language]
  );

  const groups = useMemo(() => {
    const patientItems: ResultItem[] = response.patients.map((patient) => ({
      key: `patient-${patient.id}`,
      group: 'patients',
      label: `${patient.firstName} ${patient.lastName}`.trim(),
      meta: [
        patient.uid ? `UID ${patient.uid}` : null,
        patient.birthDate ? formatDate(patient.birthDate) : null,
        patient.email ?? patient.phoneNumber
      ]
        .filter(Boolean)
        .join(' · '),
      onSelect: () => navigate(`/patients/${patient.id}`)
    }));

    const visitItems: ResultItem[] = response.visits.map((visit) => ({
      key: `visit-${visit.id}`,
      group: 'visits',
      label: t('globalSearch.visitLabel', { date: formatDate(visit.visitDate) }),
      meta: [
        visit.patientName,
        visit.hasIntakeForm ? t('globalSearch.hasIntake') : t('globalSearch.noIntake'),
        visit.note
      ]
        .filter(Boolean)
        .join(' · '),
      onSelect: () => navigate(`/patients/${visit.patientId}/visits/${visit.id}`)
    }));

    const documentItems: ResultItem[] = response.documents.map((document) => ({
      key: `document-${document.documentId}`,
      group: 'documents',
      label: document.fileName,
      meta: [document.patientName, formatDate(document.uploadedAt), document.snippet]
        .filter(Boolean)
        .join(' · '),
      onSelect: () => navigate(`/patients/${document.patientId}`)
    }));

    const evaluationItems: ResultItem[] = response.evaluations.map((evaluation) => ({
      key: `evaluation-${evaluation.caseId}`,
      group: 'evaluations',
      label: evaluation.title,
      meta: [
        evaluation.side,
        evaluation.posture,
        t('globalSearch.reviewCount', { count: evaluation.reviewCount })
      ]
        .filter(Boolean)
        .join(' · '),
      onSelect: () => navigate(`/vyhodnocovani?case=${evaluation.caseId}`)
    }));

    const employeeItems: ResultItem[] = response.employees.map((employee) => ({
      key: `employee-${employee.id}`,
      group: 'employees',
      label: employee.fullName,
      meta: [employee.email, ...employee.roles].filter(Boolean).join(' · '),
      onSelect: () => navigate('/admin/employees')
    }));

    return [
      { key: 'patients' as const, items: patientItems },
      { key: 'visits' as const, items: visitItems },
      { key: 'documents' as const, items: documentItems },
      { key: 'evaluations' as const, items: evaluationItems },
      { key: 'employees' as const, items: employeeItems }
    ].filter((group) => group.items.length > 0);
  }, [response, navigate, t, formatDate]);

  const flatItems = useMemo(() => groups.flatMap((group) => group.items), [groups]);

  const handleSelect = (item: ResultItem) => {
    close();
    item.onSelect();
  };

  const handleInputKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      close();
      return;
    }
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setActiveIndex((index) => Math.min(index + 1, Math.max(flatItems.length - 1, 0)));
      return;
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActiveIndex((index) => Math.max(index - 1, 0));
      return;
    }
    if (event.key === 'Enter' && flatItems[activeIndex]) {
      event.preventDefault();
      handleSelect(flatItems[activeIndex]!);
    }
  };

  let itemIndex = -1;

  return (
    <>
      <button
        type="button"
        className="shell-search-trigger"
        onClick={() => setOpen(true)}
        aria-label={t('globalSearch.trigger')}
      >
        <span aria-hidden="true">⌕</span>
        <span className="shell-search-trigger-label">{t('globalSearch.trigger')}</span>
        <kbd>Ctrl K</kbd>
      </button>

      {open &&
        createPortal(
          <div className="global-search-overlay" onMouseDown={close}>
            <div
              className="global-search-dialog"
              role="dialog"
              aria-modal="true"
              aria-label={t('globalSearch.trigger')}
              onMouseDown={(event) => event.stopPropagation()}
            >
              <div className="global-search-input-row">
                <span className="global-search-icon" aria-hidden="true">
                  ⌕
                </span>
                <input
                  ref={inputRef}
                  value={query}
                  onChange={(event) => setQuery(event.target.value)}
                  onKeyDown={handleInputKeyDown}
                  placeholder={t('globalSearch.placeholder')}
                  aria-label={t('globalSearch.placeholder')}
                  autoComplete="off"
                  spellCheck={false}
                />
                <button
                  type="button"
                  className="global-search-close"
                  onClick={close}
                  aria-label={t('globalSearch.close')}
                >
                  Esc
                </button>
              </div>

              <div className="global-search-results">
                {loading && <div className="global-search-state">{t('common.loading')}</div>}
                {!loading && error && <div className="global-search-state error">{error}</div>}
                {!loading && !error && query.trim().length < 2 && (
                  <div className="global-search-state">{t('globalSearch.hint')}</div>
                )}
                {!loading && !error && query.trim().length >= 2 && groups.length === 0 && (
                  <div className="global-search-state">{t('globalSearch.noResults')}</div>
                )}

                {!loading &&
                  !error &&
                  groups.map((group) => (
                    <section key={group.key} className="global-search-group">
                      <h3>{t(`globalSearch.groups.${group.key}`)}</h3>
                      <ul role="listbox">
                        {group.items.map((item) => {
                          itemIndex += 1;
                          const index = itemIndex;
                          return (
                            <li key={item.key} role="option" aria-selected={index === activeIndex}>
                              <button
                                type="button"
                                className={`global-search-item${index === activeIndex ? ' active' : ''}`}
                                onMouseEnter={() => setActiveIndex(index)}
                                onClick={() => handleSelect(item)}
                              >
                                <span className="global-search-item-label">{item.label}</span>
                                <span className="global-search-item-meta">{item.meta}</span>
                              </button>
                            </li>
                          );
                        })}
                      </ul>
                    </section>
                  ))}
              </div>

              <div className="global-search-footer">
                <span>↑↓ {t('globalSearch.navigate')}</span>
                <span>↵ {t('globalSearch.open')}</span>
                <span>Esc {t('globalSearch.close')}</span>
              </div>
            </div>
          </div>,
          document.body
        )}
    </>
  );
};

export default GlobalSearch;
