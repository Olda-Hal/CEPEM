import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AppHeader } from '../components/AppHeader';
import { apiClient } from '../utils/api';
import { ExaminationSearchResponse, ExaminationListItem, Hospital } from '../types';
import './ExaminationsPage.css';

const ITEMS_PER_PAGE = 50;

interface ExaminationFilterState {
  search: string;
  hospitalId: string;
  hospitalCountryScopeId: string;
  patientCountryCode: string;
  examinationCountryCode: string;
  from: string;
  to: string;
}

const DEFAULT_FILTERS: ExaminationFilterState = {
  search: '',
  hospitalId: '',
  hospitalCountryScopeId: '',
  patientCountryCode: '',
  examinationCountryCode: '',
  from: '',
  to: ''
};

export const ExaminationsPage: React.FC = () => {
  const { t, i18n } = useTranslation();

  const [items, setItems] = useState<ExaminationListItem[]>([]);
  const [hospitals, setHospitals] = useState<Hospital[]>([]);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(0);

  const [filters, setFilters] = useState<ExaminationFilterState>(DEFAULT_FILTERS);
  const [appliedFilters, setAppliedFilters] = useState<ExaminationFilterState>(DEFAULT_FILTERS);

  useEffect(() => {
    loadHospitals();
  }, []);

  useEffect(() => {
    loadExaminations();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [appliedFilters, page, i18n.language]);

  const loadHospitals = async () => {
    try {
      const data = await apiClient.get<Hospital[]>('/api/hospitals');
      setHospitals(data.filter(hospital => hospital.active !== false));
    } catch (loadError) {
      console.error('Error loading hospitals for examinations page:', loadError);
    }
  };

  const buildQueryString = (selectedFilters: ExaminationFilterState, selectedPage: number) => {
    const query = new URLSearchParams();
    query.set('language', i18n.language || 'cs');
    query.set('page', selectedPage.toString());
    query.set('limit', ITEMS_PER_PAGE.toString());

    if (selectedFilters.search.trim()) query.set('search', selectedFilters.search.trim());
    if (selectedFilters.hospitalId) query.set('hospitalId', selectedFilters.hospitalId);
    if (selectedFilters.hospitalCountryScopeId) query.set('hospitalCountryScopeId', selectedFilters.hospitalCountryScopeId);
    if (selectedFilters.patientCountryCode.trim()) query.set('patientCountryCode', selectedFilters.patientCountryCode.trim().toUpperCase());
    if (selectedFilters.examinationCountryCode.trim()) query.set('examinationCountryCode', selectedFilters.examinationCountryCode.trim().toUpperCase());
    if (selectedFilters.from) query.set('from', new Date(selectedFilters.from).toISOString());
    if (selectedFilters.to) query.set('to', new Date(selectedFilters.to).toISOString());

    return query.toString();
  };

  const loadExaminations = async () => {
    setLoading(true);
    setError(null);

    try {
      const query = buildQueryString(appliedFilters, page);
      const response = await apiClient.get<ExaminationSearchResponse>(`/api/examinations?${query}`);
      setItems(response.items);
      setTotalCount(response.totalCount);
    } catch (loadError) {
      console.error('Error loading examinations:', loadError);
      setError(t('examinations.errors.loading'));
    } finally {
      setLoading(false);
    }
  };

  const formatDateTime = (value: string) => {
    const locale = i18n.language === 'cs' ? 'cs-CZ' : 'en-US';
    return new Date(value).toLocaleString(locale);
  };

  const handleApplyFilters = () => {
    setPage(0);
    setAppliedFilters(filters);
  };

  const handleResetFilters = () => {
    setFilters(DEFAULT_FILTERS);
    setPage(0);
    setAppliedFilters(DEFAULT_FILTERS);
  };

  const handleExport = async () => {
    setExporting(true);

    try {
      const query = buildQueryString(appliedFilters, 0);
      const blob = await apiClient.getBlob(`/api/examinations/export?${query}`);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
      link.href = url;
      link.download = `examinations_export_${timestamp}.zip`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
    } catch (exportError) {
      console.error('Error exporting examinations:', exportError);
      setError(t('examinations.errors.export'));
    } finally {
      setExporting(false);
    }
  };

  const totalPages = Math.max(1, Math.ceil(totalCount / ITEMS_PER_PAGE));

  return (
    <div className="examinations-container">
      <AppHeader sectionTitle={t('examinations.title')} />

      <div className="examinations-content">
        <div className="examinations-filters">
          <div className="filter-row">
            <input
              type="text"
              className="filter-input"
              placeholder={t('examinations.filters.searchPlaceholder')}
              value={filters.search}
              onChange={(event) => setFilters(prev => ({ ...prev, search: event.target.value }))}
            />

            <select
              className="filter-input"
              value={filters.hospitalId}
              onChange={(event) => setFilters(prev => ({ ...prev, hospitalId: event.target.value }))}
            >
              <option value="">{t('examinations.filters.allCenters')}</option>
              {hospitals.map(hospital => (
                <option key={hospital.id} value={hospital.id}>
                  {hospital.name || `${t('examinations.centerFallback')} #${hospital.id}`}
                </option>
              ))}
            </select>

            <input
              type="number"
              className="filter-input"
              placeholder={t('examinations.filters.hospitalCountryScopeId')}
              value={filters.hospitalCountryScopeId}
              onChange={(event) => setFilters(prev => ({ ...prev, hospitalCountryScopeId: event.target.value }))}
            />
          </div>

          <div className="filter-row">
            <input
              type="text"
              maxLength={2}
              className="filter-input"
              placeholder={t('examinations.filters.patientCountryCode')}
              value={filters.patientCountryCode}
              onChange={(event) => setFilters(prev => ({ ...prev, patientCountryCode: event.target.value }))}
            />
            <input
              type="text"
              maxLength={2}
              className="filter-input"
              placeholder={t('examinations.filters.examinationCountryCode')}
              value={filters.examinationCountryCode}
              onChange={(event) => setFilters(prev => ({ ...prev, examinationCountryCode: event.target.value }))}
            />
            <input
              type="datetime-local"
              className="filter-input"
              value={filters.from}
              onChange={(event) => setFilters(prev => ({ ...prev, from: event.target.value }))}
            />
            <input
              type="datetime-local"
              className="filter-input"
              value={filters.to}
              onChange={(event) => setFilters(prev => ({ ...prev, to: event.target.value }))}
            />
          </div>

          <div className="filter-actions">
            <button className="page-button primary" onClick={handleApplyFilters}>
              {t('examinations.filters.apply')}
            </button>
            <button className="page-button" onClick={handleResetFilters}>
              {t('examinations.filters.reset')}
            </button>
            <button className="page-button export" onClick={handleExport} disabled={exporting || loading}>
              {exporting ? t('examinations.exporting') : t('examinations.export')}
            </button>
          </div>
        </div>

        {error && <div className="error-message">{error}</div>}

        <div className="examinations-summary">
          {t('examinations.totalFound', { count: totalCount })}
        </div>

        {loading ? (
          <div className="loading-container">
            <div className="loading-spinner"></div>
            <p>{t('common.loading')}</p>
          </div>
        ) : (
          <>
            <div className="examinations-table-wrapper">
              <table className="examinations-table">
                <thead>
                  <tr>
                    <th>{t('examinations.table.happenedAt')}</th>
                    <th>{t('examinations.table.patient')}</th>
                    <th>{t('examinations.table.examinationType')}</th>
                    <th>{t('examinations.table.center')}</th>
                    <th>{t('examinations.table.centerCountryScopeId')}</th>
                    <th>{t('examinations.table.patientCountry')}</th>
                    <th>{t('examinations.table.examinationCountry')}</th>
                    <th>{t('examinations.table.documents')}</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map(item => (
                    <tr key={item.id}>
                      <td>{formatDateTime(item.happenedAt)}</td>
                      <td>{item.patientFirstName} {item.patientLastName}</td>
                      <td>{item.examinationTypeName}</td>
                      <td>{item.hospitalName || '-'}</td>
                      <td>{item.hospitalCountryScopeId ?? '-'}</td>
                      <td>{item.patientCountryCode || '-'}</td>
                      <td>{item.examinationCountryCode || '-'}</td>
                      <td>
                        <div className="doc-cell">
                          <strong>{item.documentCount}</strong>
                          {item.documents.length > 0 && (
                            <div className="doc-names">
                              {item.documents.map(doc => (
                                <span key={doc.id}>{doc.fileName}</span>
                              ))}
                            </div>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {items.length === 0 && (
              <div className="empty-state">{t('examinations.empty')}</div>
            )}

            <div className="pagination">
              <button
                className="page-button"
                disabled={page <= 0}
                onClick={() => setPage(prev => Math.max(0, prev - 1))}
              >
                {t('examinations.pagination.previous')}
              </button>
              <span>
                {t('examinations.pagination.page', { current: page + 1, total: totalPages })}
              </span>
              <button
                className="page-button"
                disabled={page + 1 >= totalPages}
                onClick={() => setPage(prev => prev + 1)}
              >
                {t('examinations.pagination.next')}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
};
