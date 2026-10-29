import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../../utils/api';
import { VisitListItem } from '../../types';
import { BaseModal } from '../BaseModal';

interface PatientVisitsSectionProps {
  patientId: number;
}

/**
 * "Návštěvy" — the dated visit list replacing the old Documents/Events/Termíny
 * sections on the patient page. Rows open the visit detail page; "Přidat
 * návštěvu" asks only for the date (and optional note) first.
 */
export const PatientVisitsSection: React.FC<PatientVisitsSectionProps> = ({ patientId }) => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();

  const [visits, setVisits] = useState<VisitListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [showAddModal, setShowAddModal] = useState(false);
  const [visitDate, setVisitDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [note, setNote] = useState('');
  const [saving, setSaving] = useState(false);

  const loadVisits = useCallback(async () => {
    try {
      const data = await apiClient.get<VisitListItem[]>(`/api/patients/${patientId}/visits`);
      setVisits(data);
      setError(null);
    } catch (err) {
      console.error(err);
      setError(t('visits.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [patientId, t]);

  useEffect(() => {
    setLoading(true);
    void loadVisits();
  }, [loadVisits]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!visitDate) return;
    setSaving(true);
    try {
      const created = await apiClient.post<VisitListItem>(`/api/patients/${patientId}/visits`, {
        visitDate,
        note: note || undefined
      });
      setShowAddModal(false);
      setNote('');
      navigate(`/patients/${patientId}/visits/${created.id}`);
    } catch (err) {
      console.error(err);
      setError(err instanceof Error && err.message ? err.message : t('visits.createFailed'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="patient-visits-card patient-documents-card">
      <div className="card-header">
        <h2>
          {t('visits.title')} ({visits.length})
        </h2>
        <button className="btn btn-primary" onClick={() => setShowAddModal(true)}>
          + {t('visits.addVisit')}
        </button>
      </div>

      {error && <div className="alert alert-error">{error}</div>}
      {loading && <div className="no-data">{t('common.loading')}</div>}

      {!loading && visits.length === 0 && (
        <div className="no-data">
          <p>{t('visits.empty')}</p>
        </div>
      )}

      {!loading && visits.length > 0 && (
        <div className="visits-list">
          {visits.map((visit) => (
            <button
              key={visit.id}
              className="visit-item"
              onClick={() => navigate(`/patients/${patientId}/visits/${visit.id}`)}
            >
              <span className="visit-date">
                {new Date(visit.visitDate).toLocaleDateString(i18n.language)}
              </span>
              <span className="visit-note">{visit.note || '—'}</span>
              <span className="visit-badges">
                <span className="status-badge status-reserved">
                  {t('visits.documentsCount', { count: visit.documentCount })}
                </span>
                {visit.hasIntakeForm && (
                  <span className="status-badge status-active">{t('visits.intakeBadge')}</span>
                )}
              </span>
              <span className="administration-menu-chevron">›</span>
            </button>
          ))}
        </div>
      )}

      {showAddModal && (
        <BaseModal title={t('visits.addVisitTitle')} onClose={() => setShowAddModal(false)}>
          <form onSubmit={handleCreate} className="create-employee-form">
            <div className="form-group">
              <label htmlFor="visitDate">{t('visits.visitDate')} *</label>
              <input
                type="date"
                id="visitDate"
                value={visitDate}
                onChange={(e) => setVisitDate(e.target.value)}
                required
              />
            </div>
            <div className="form-group">
              <label htmlFor="visitNote">{t('visits.visitNote')}</label>
              <textarea
                id="visitNote"
                value={note}
                onChange={(e) => setNote(e.target.value)}
                rows={3}
                placeholder={t('visits.visitNotePlaceholder')}
              />
            </div>
            <div className="modal-footer">
              <button
                type="button"
                className="cancel-button"
                onClick={() => setShowAddModal(false)}
              >
                {t('common.cancel')}
              </button>
              <button type="submit" className="submit-button" disabled={saving || !visitDate}>
                {saving ? t('common.loading') : t('visits.createVisit')}
              </button>
            </div>
          </form>
        </BaseModal>
      )}
    </div>
  );
};

export default PatientVisitsSection;
