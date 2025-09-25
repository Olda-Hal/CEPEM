import React, { useState, useEffect, useCallback, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { Patient, PatientSearchResponse } from '../types';
import { apiClient } from '../utils/api';
import { CreatePatientModal } from '../components/CreatePatientModal';
import { AppHeader } from '../components/AppHeader';
import './PatientsPage.css';

const ITEMS_PER_PAGE = 20;

export const PatientsPage: React.FC = () => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const [patients, setPatients] = useState<Patient[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [hasMore, setHasMore] = useState(true);
  const [page, setPage] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);
  
  const observer = useRef<IntersectionObserver | null>(null);
  const lastPatientElementRef = useRef<HTMLDivElement | null>(null);

  const loadPatients = useCallback(async (pageNum: number, query: string = '', reset: boolean = false) => {
    try {
      if (pageNum === 0) setLoading(true);
      else setLoadingMore(true);

      const queryParams = new URLSearchParams({
        page: pageNum.toString(),
        limit: ITEMS_PER_PAGE.toString(),
        search: query,
        sortBy: 'lastName',
        sortOrder: 'asc'
      });

      const response = await apiClient.get<PatientSearchResponse>(`/api/patients/search?${queryParams}`);

      if (reset || pageNum === 0) {
        setPatients(response.patients);
      } else {
        setPatients(prev => [...prev, ...response.patients]);
      }

      setHasMore(response.hasMore);
      setError(null);
    } catch (error) {
      console.error('Error loading patients:', error);
      setError(t('errors.loadingPatients'));
    } finally {
      setLoading(false);
      setLoadingMore(false);
    }
  }, [t]);

  const lastPatientElementCallback = useCallback((node: HTMLDivElement | null) => {
    if (loadingMore) return;
    if (observer.current) observer.current.disconnect();
    
    observer.current = new IntersectionObserver(entries => {
      if (entries[0].isIntersecting && hasMore && !loading) {
        const nextPage = page + 1;
        setPage(nextPage);
        loadPatients(nextPage, searchQuery);
      }
    });
    
    if (node) observer.current.observe(node);
    lastPatientElementRef.current = node;
  }, [loadingMore, hasMore, loading, page, searchQuery, loadPatients]);

  useEffect(() => {
    loadPatients(0, searchQuery);
  }, []);

  useEffect(() => {
    const timeoutId = setTimeout(() => {
      setPage(0);
      loadPatients(0, searchQuery, true);
    }, 300);

    return () => clearTimeout(timeoutId);
  }, [searchQuery, loadPatients]);

  const formatDate = (dateString: string) => {
    const locale = i18n.language === 'cs' ? 'cs-CZ' : 'en-US';
    return new Date(dateString).toLocaleDateString(locale);
  };

  const handlePatientCreated = (patientId?: number) => {
    if (patientId) {
      // Navigate to patient detail page
      navigate(`/patients/${patientId}`);
    } else {
      // Fallback: refresh the patients list
      setPage(0);
      loadPatients(0, searchQuery, true);
    }
  };

  const handleViewPatientDetail = (patientId: number) => {
    navigate(`/patients/${patientId}`);
  };

  return (
    <div className="patients-container">
      <AppHeader 
        sectionTitle={t('patients.title')}
      >
        <button 
          className="add-patient-button"
          onClick={() => setShowCreateModal(true)}
        >
          + {t('patients.addPatient')}
        </button>
      </AppHeader>
      
      <div className="search-section">
        <div className="search-bar">
          <input
            type="text"
            placeholder={t('patients.searchPlaceholder')}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="search-input"
          />
          <div className="search-icon">🔍</div>
        </div>
      </div>

      <div className="patients-content">
        {error && (
          <div className="error-message">
            {error}
          </div>
        )}

        {loading && patients.length === 0 ? (
          <div className="loading-container">
            <div className="loading-spinner"></div>
            <p>{t('patients.loading')}</p>
          </div>
        ) : (
          <>
            <div className="patients-stats">
              <span className="total-count">
                {t('patients.totalFound', { count: patients.length })}
              </span>
            </div>

            <div className="patients-grid">
              {patients.map((patient, index) => (
                <div
                  key={patient.id}
                  className="patient-card"
                  ref={index === patients.length - 1 ? lastPatientElementCallback : null}
                >
                  <div className="patient-header">
                    <h3 
                      className="patient-name"
                      title={`${patient.titleBefore ? `${patient.titleBefore} ` : ''}${patient.lastName}, ${patient.firstName}${patient.titleAfter ? `, ${patient.titleAfter}` : ''}`}
                    >
                      {patient.titleBefore && (
                        <div className="name-title-before">{patient.titleBefore}</div>
                      )}
                      <div className="name-main">
                        <div className="name-firstname">{patient.firstName}</div>
                        <div className="name-lastname">{patient.lastName}</div>
                      </div>
                      {patient.titleAfter && (
                        <div className="name-title-after">{patient.titleAfter}</div>
                      )}
                    </h3>
                    <span className="patient-id">ID: {patient.id}</span>
                  </div>
                  
                  <div className="patient-details">
                    <div className="detail-row">
                      <span className="detail-label">{t('patients.birthDate')}:</span>
                      <span className="detail-value">{formatDate(patient.birthDate)}</span>
                    </div>
                    
                    <div className="detail-row">
                      <span className="detail-label">{t('patients.uid')}:</span>
                      <span className="detail-value">{patient.uid}</span>
                    </div>
                    
                    <div className="detail-row">
                      <span className="detail-label">{t('patients.insuranceNumber')}:</span>
                      <span className="detail-value">{patient.insuranceNumber}</span>
                    </div>
                    
                    {patient.phoneNumber && (
                      <div className="detail-row">
                        <span className="detail-label">{t('patients.phone')}:</span>
                        <span className="detail-value">{patient.phoneNumber}</span>
                      </div>
                    )}
                    
                    {patient.email && (
                      <div className="detail-row">
                        <span className="detail-label">{t('patients.email')}:</span>
                        <span className="detail-value">{patient.email}</span>
                      </div>
                    )}
                    
                    <div className="detail-row">
                      <span className="detail-label">{t('patients.gender')}:</span>
                      <span className="detail-value">
                        {patient.gender === 'M' ? t('patients.male') : t('patients.female')}
                      </span>
                    </div>
                  </div>
                  
                  <div className="patient-actions">
                    <button 
                      className="action-button primary"
                      onClick={() => handleViewPatientDetail(patient.id)}
                    >
                      {t('patients.viewDetails')}
                    </button>
                    <button className="action-button secondary">
                      {t('patients.editPatient')}
                    </button>
                  </div>
                </div>
              ))}
            </div>

            {loadingMore && (
              <div className="loading-more">
                <div className="loading-spinner small"></div>
                <p>{t('patients.loadingMore')}</p>
              </div>
            )}

            {!hasMore && patients.length > 0 && (
              <div className="end-message">
                {t('patients.allLoaded')}
              </div>
            )}

            {!loading && patients.length === 0 && (
              <div className="no-results">
                <div className="no-results-icon">👥</div>
                <h3>{t('patients.noResults')}</h3>
                <p>{t('patients.noResultsDescription')}</p>
              </div>
            )}
          </>
        )}
      </div>

      <CreatePatientModal
        isOpen={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        onPatientCreated={handlePatientCreated}
      />
    </div>
  );
};
