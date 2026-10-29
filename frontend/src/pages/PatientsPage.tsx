import React, { useState, useEffect, useCallback, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { useInfiniteQuery, useQueryClient } from '@tanstack/react-query';
import { MdDeleteOutline, MdGroups, MdRestoreFromTrash, MdSearch } from 'react-icons/md';
import { useAuth } from '../contexts/AuthContext';
import { hasRole, isAdmin } from '../utils/roles';
import { PatientSearchResponse } from '../types';
import { apiClient } from '../utils/api';
import { CreatePatientModal } from '../components/CreatePatientModal';
import { BaseModal } from '../components/BaseModal';
import './PatientsPage.css';

const ITEMS_PER_PAGE = 20;

export const PatientsPage: React.FC = () => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const canSeeDeleted =
    isAdmin(user) || hasRole(user, 'Center Admin') || hasRole(user, 'Country Admin');
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showDeleted, setShowDeleted] = useState(false);
  const [confirmDeleteId, setConfirmDeleteId] = useState<number | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const observer = useRef<IntersectionObserver | null>(null);
  const lastPatientElementRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    const timeoutId = setTimeout(() => setDebouncedQuery(searchQuery), 300);
    return () => clearTimeout(timeoutId);
  }, [searchQuery]);

  const { data, error, isLoading, isFetchingNextPage, hasNextPage, fetchNextPage } =
    useInfiniteQuery({
      queryKey: ['patients', debouncedQuery, showDeleted],
      queryFn: async ({ pageParam = 0 }) => {
        const queryParams = new URLSearchParams({
          page: pageParam.toString(),
          limit: ITEMS_PER_PAGE.toString(),
          search: debouncedQuery,
          sortBy: 'lastName',
          sortOrder: 'asc',
          includeDeleted: showDeleted ? 'true' : 'false'
        });
        return apiClient.get<PatientSearchResponse>(`/api/patients/search?${queryParams}`);
      },
      initialPageParam: 0,
      getNextPageParam: (lastPage, allPages) => (lastPage.hasMore ? allPages.length : undefined)
    });

  const handleDelete = async () => {
    if (confirmDeleteId == null) return;
    try {
      await apiClient.delete(`/api/patients/${confirmDeleteId}`);
      setConfirmDeleteId(null);
      queryClient.invalidateQueries({ queryKey: ['patients'] });
    } catch (deleteError) {
      console.error('Failed to delete patient', deleteError);
      setActionError(t('patients.deleteFailed'));
    }
  };

  const handleRestore = async (patientId: number) => {
    try {
      await apiClient.post(`/api/patients/${patientId}/restore`, {});
      queryClient.invalidateQueries({ queryKey: ['patients'] });
    } catch (restoreError) {
      console.error('Failed to restore patient', restoreError);
      setActionError(t('patients.restoreFailed'));
    }
  };

  const patients = data?.pages.flatMap((page) => page.patients) ?? [];
  const totalCount = data?.pages[0]?.totalCount ?? 0;

  const lastPatientElementCallback = useCallback(
    (node: HTMLDivElement | null) => {
      if (isFetchingNextPage) return;
      if (observer.current) observer.current.disconnect();

      observer.current = new IntersectionObserver((entries) => {
        if (entries[0]?.isIntersecting && hasNextPage && !isLoading) {
          fetchNextPage();
        }
      });

      if (node) observer.current.observe(node);
      lastPatientElementRef.current = node;
    },
    [hasNextPage, isLoading, isFetchingNextPage, fetchNextPage]
  );

  const formatDate = (dateString: string) => {
    const locale = i18n.language === 'cs' ? 'cs-CZ' : 'en-US';
    return new Date(dateString).toLocaleDateString(locale);
  };

  const handlePatientCreated = (patientId?: number) => {
    if (patientId) {
      navigate(`/patients/${patientId}`);
    } else {
      queryClient.invalidateQueries({ queryKey: ['patients'] });
    }
  };

  const handleViewPatientDetail = (patientId: number) => {
    navigate(`/patients/${patientId}`);
  };

  const handleEditPatient = (patientId: number) => {
    navigate(`/patients/${patientId}/edit`);
  };

  return (
    <div className="patients-container">
      <div className="page-heading">
        <h1 className="page-title">{t('patients.title')}</h1>
        <div className="page-heading-actions">
          <button className="btn btn-primary" onClick={() => navigate('/patients/add')}>
            + {t('patients.addPatient')}
          </button>
        </div>
      </div>

      <div className="search-section">
        <div className="search-bar">
          <input
            type="text"
            placeholder={t('patients.searchPlaceholder')}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="search-input"
          />
          <div className="search-icon">
            <MdSearch size={22} />
          </div>
        </div>
        {canSeeDeleted && (
          <button
            type="button"
            className={`show-deleted-toggle${showDeleted ? ' active' : ''}`}
            onClick={() => setShowDeleted((prev) => !prev)}
            aria-pressed={showDeleted}
            title={t('patients.showDeleted')}
          >
            <MdRestoreFromTrash size={19} />
            <span>{t('patients.showDeleted')}</span>
          </button>
        )}
      </div>

      <div className="patients-content">
        {error && <div className="error-message">{t('errors.loadingPatients')}</div>}
        {actionError && <div className="error-message">{actionError}</div>}

        {isLoading && patients.length === 0 ? (
          <div className="patients-grid" data-testid="patients-skeleton" aria-busy="true">
            {Array.from({ length: 6 }).map((_, index) => (
              <div key={index} className="patient-card skeleton-card">
                <div className="skeleton-line skeleton-title" />
                <div className="skeleton-line" />
                <div className="skeleton-line" />
                <div className="skeleton-line skeleton-short" />
              </div>
            ))}
          </div>
        ) : (
          <>
            <div className="patients-stats">
              <span className="total-count">{t('patients.totalFound', { count: totalCount })}</span>
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
                    <span className="patient-id">UID: {patient.uid}</span>
                  </div>

                  <div className="patient-details">
                    <div className="detail-row">
                      <span className="detail-label">{t('patients.birthDate')}:</span>
                      <span className="detail-value">{formatDate(patient.birthDate)}</span>
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
                    <button
                      className="action-button secondary"
                      onClick={() => handleEditPatient(patient.id)}
                    >
                      {t('patients.editPatient')}
                    </button>
                    {patient.isDeleted ? (
                      <button
                        className="action-button secondary restore"
                        onClick={() => handleRestore(patient.id)}
                      >
                        {t('patients.restore')}
                      </button>
                    ) : (
                      patient.canDelete && (
                        <button
                          className="action-button icon danger"
                          onClick={() => setConfirmDeleteId(patient.id)}
                          title={t('patients.deleteTitle')}
                          aria-label={t('patients.deleteTitle')}
                        >
                          <MdDeleteOutline size={20} />
                        </button>
                      )
                    )}
                  </div>
                </div>
              ))}
            </div>

            {isFetchingNextPage && (
              <div className="loading-more">
                <div className="loading-spinner small"></div>
                <p>{t('patients.loadingMore')}</p>
              </div>
            )}

            {!hasNextPage && patients.length > 0 && (
              <div className="end-message">{t('patients.allLoaded')}</div>
            )}

            {!isLoading && patients.length === 0 && (
              <div className="no-results">
                <div className="no-results-icon">
                  <MdGroups size={64} />
                </div>
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

      {confirmDeleteId != null && (
        <BaseModal title={t('patients.deleteTitle')} onClose={() => setConfirmDeleteId(null)}>
          <p>{t('patients.deleteConfirm')}</p>
          <div className="modal-actions">
            <button className="btn btn-secondary" onClick={() => setConfirmDeleteId(null)}>
              {t('common.cancel')}
            </button>
            <button className="btn btn-danger" onClick={handleDelete}>
              {t('patients.deleteAction')}
            </button>
          </div>
        </BaseModal>
      )}
    </div>
  );
};
