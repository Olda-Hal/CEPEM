import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { EventOptions, CreateEventRequest } from '../types';
import { apiClient } from '../utils/api';
import './AddEventModal.css';

interface AddEventModalProps {
  isOpen: boolean;
  onClose: () => void;
  patientId: number;
  onEventAdded: () => void;
}

const AddEventModal: React.FC<AddEventModalProps> = ({
  isOpen,
  onClose,
  patientId,
  onEventAdded
}) => {
  const { t } = useTranslation();
  const [eventOptions, setEventOptions] = useState<EventOptions | null>(null);
  const [loading, setLoading] = useState(false);
  const [formData, setFormData] = useState<CreateEventRequest>({
    patientId,
    eventTypeId: 0,
    happenedAt: new Date().toISOString().slice(0, 16),
    happenedTo: '',
    comment: '',
    drugIds: [],
    examinationTypeIds: [],
    symptomIds: [],
    injuryTypeIds: [],
    vaccineTypeIds: [],
    isPregnant: undefined,
    pregnancyResult: undefined
  });

  useEffect(() => {
    if (isOpen) {
      loadEventOptions();
    }
  }, [isOpen]);

  const loadEventOptions = async () => {
    try {
      setLoading(true);
      const response = await apiClient.get<EventOptions>('/api/events/options');
      setEventOptions(response);
    } catch (error) {
      console.error('Error loading event options:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.eventTypeId) {
      return;
    }

    try {
      setLoading(true);
      await apiClient.post('/api/events', formData);
      onEventAdded();
      onClose();
      resetForm();
    } catch (error) {
      console.error('Error creating event:', error);
    } finally {
      setLoading(false);
    }
  };

  const resetForm = () => {
    setFormData({
      patientId,
      eventTypeId: 0,
      happenedAt: new Date().toISOString().slice(0, 16),
      happenedTo: '',
      comment: '',
      drugIds: [],
      examinationTypeIds: [],
      symptomIds: [],
      injuryTypeIds: [],
      vaccineTypeIds: [],
      isPregnant: undefined,
      pregnancyResult: undefined
    });
  };

  const handleCheckboxChange = (
    field: 'drugIds' | 'examinationTypeIds' | 'symptomIds' | 'injuryTypeIds' | 'vaccineTypeIds',
    id: number,
    checked: boolean
  ) => {
    setFormData(prev => ({
      ...prev,
      [field]: checked
        ? [...prev[field], id]
        : prev[field].filter(existingId => existingId !== id)
    }));
  };

  if (!isOpen) return null;

  return (
    <div className="modal-overlay">
      <div className="modal-content add-event-modal">
        <div className="modal-header">
          <h2>{t('events.addEvent')}</h2>
          <button className="close-button" onClick={onClose}>×</button>
        </div>

        {loading && !eventOptions ? (
          <div className="loading-container">
            <p>{t('common.loading')}</p>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="event-form">
            <div className="form-section">
              <h3>{t('events.basicInfo')}</h3>
              
              <div className="form-group">
                <label htmlFor="eventType">{t('events.eventType')} *</label>
                <select
                  id="eventType"
                  value={formData.eventTypeId}
                  onChange={(e) => setFormData(prev => ({ ...prev, eventTypeId: parseInt(e.target.value) }))}
                  required
                >
                  <option value={0}>{t('events.selectEventType')}</option>
                  {eventOptions?.eventTypes.map(type => (
                    <option key={type.id} value={type.id}>{type.name}</option>
                  ))}
                </select>
              </div>

              <div className="form-group">
                <label htmlFor="happenedAt">{t('events.happenedAt')} *</label>
                <input
                  type="datetime-local"
                  id="happenedAt"
                  value={formData.happenedAt}
                  onChange={(e) => setFormData(prev => ({ ...prev, happenedAt: e.target.value }))}
                  required
                />
              </div>

              <div className="form-group">
                <label htmlFor="happenedTo">{t('events.happenedTo')}</label>
                <input
                  type="datetime-local"
                  id="happenedTo"
                  value={formData.happenedTo}
                  onChange={(e) => setFormData(prev => ({ ...prev, happenedTo: e.target.value }))}
                />
              </div>

              <div className="form-group">
                <label htmlFor="comment">{t('events.comment')}</label>
                <textarea
                  id="comment"
                  value={formData.comment}
                  onChange={(e) => setFormData(prev => ({ ...prev, comment: e.target.value }))}
                  rows={3}
                  placeholder={t('events.commentPlaceholder')}
                />
              </div>
            </div>

            {eventOptions && (
              <>
                {eventOptions.drugs.length > 0 && (
                  <div className="form-section">
                    <h3>{t('events.drugs')}</h3>
                    <div className="checkbox-grid">
                      {eventOptions.drugs.map(drug => (
                        <label key={drug.id} className="checkbox-item">
                          <input
                            type="checkbox"
                            checked={formData.drugIds.includes(drug.id)}
                            onChange={(e) => handleCheckboxChange('drugIds', drug.id, e.target.checked)}
                          />
                          <span>{drug.name}</span>
                        </label>
                      ))}
                    </div>
                  </div>
                )}

                {eventOptions.examinationTypes.length > 0 && (
                  <div className="form-section">
                    <h3>{t('events.examinations')}</h3>
                    <div className="checkbox-grid">
                      {eventOptions.examinationTypes.map(exam => (
                        <label key={exam.id} className="checkbox-item">
                          <input
                            type="checkbox"
                            checked={formData.examinationTypeIds.includes(exam.id)}
                            onChange={(e) => handleCheckboxChange('examinationTypeIds', exam.id, e.target.checked)}
                          />
                          <span>{exam.name}</span>
                        </label>
                      ))}
                    </div>
                  </div>
                )}

                {eventOptions.symptoms.length > 0 && (
                  <div className="form-section">
                    <h3>{t('events.symptoms')}</h3>
                    <div className="checkbox-grid">
                      {eventOptions.symptoms.map(symptom => (
                        <label key={symptom.id} className="checkbox-item">
                          <input
                            type="checkbox"
                            checked={formData.symptomIds.includes(symptom.id)}
                            onChange={(e) => handleCheckboxChange('symptomIds', symptom.id, e.target.checked)}
                          />
                          <span>{symptom.name}</span>
                        </label>
                      ))}
                    </div>
                  </div>
                )}

                {eventOptions.injuryTypes.length > 0 && (
                  <div className="form-section">
                    <h3>{t('events.injuries')}</h3>
                    <div className="checkbox-grid">
                      {eventOptions.injuryTypes.map(injury => (
                        <label key={injury.id} className="checkbox-item">
                          <input
                            type="checkbox"
                            checked={formData.injuryTypeIds.includes(injury.id)}
                            onChange={(e) => handleCheckboxChange('injuryTypeIds', injury.id, e.target.checked)}
                          />
                          <span>{injury.name}</span>
                        </label>
                      ))}
                    </div>
                  </div>
                )}

                {eventOptions.vaccineTypes.length > 0 && (
                  <div className="form-section">
                    <h3>{t('events.vaccines')}</h3>
                    <div className="checkbox-grid">
                      {eventOptions.vaccineTypes.map(vaccine => (
                        <label key={vaccine.id} className="checkbox-item">
                          <input
                            type="checkbox"
                            checked={formData.vaccineTypeIds.includes(vaccine.id)}
                            onChange={(e) => handleCheckboxChange('vaccineTypeIds', vaccine.id, e.target.checked)}
                          />
                          <span>{vaccine.name}</span>
                        </label>
                      ))}
                    </div>
                  </div>
                )}

                <div className="form-section">
                  <h3>{t('events.pregnancy')}</h3>
                  <div className="pregnancy-section">
                    <label className="checkbox-item">
                      <input
                        type="checkbox"
                        checked={formData.isPregnant === true}
                        onChange={(e) => setFormData(prev => ({ 
                          ...prev, 
                          isPregnant: e.target.checked ? true : undefined,
                          pregnancyResult: e.target.checked ? prev.pregnancyResult : undefined
                        }))}
                      />
                      <span>{t('events.isPregnant')}</span>
                    </label>
                    
                    {formData.isPregnant && (
                      <div className="pregnancy-result">
                        <label>{t('events.pregnancyResult')}</label>
                        <div className="radio-group">
                          <label className="radio-item">
                            <input
                              type="radio"
                              name="pregnancyResult"
                              checked={formData.pregnancyResult === true}
                              onChange={() => setFormData(prev => ({ ...prev, pregnancyResult: true }))}
                            />
                            <span>{t('events.positive')}</span>
                          </label>
                          <label className="radio-item">
                            <input
                              type="radio"
                              name="pregnancyResult"
                              checked={formData.pregnancyResult === false}
                              onChange={() => setFormData(prev => ({ ...prev, pregnancyResult: false }))}
                            />
                            <span>{t('events.negative')}</span>
                          </label>
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              </>
            )}

            <div className="modal-actions">
              <button type="button" onClick={onClose} className="cancel-button">
                {t('common.cancel')}
              </button>
              <button type="submit" disabled={loading || !formData.eventTypeId} className="save-button">
                {loading ? t('events.creating') : t('events.createEvent')}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
};

export default AddEventModal;
