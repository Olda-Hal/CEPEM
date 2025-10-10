import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { EventOptions, CreateEventRequest, DrugUseRequest } from '../types';
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
    drugUses: [],
    examinationTypeIds: [],
    symptomIds: [],
    injuryTypeIds: [],
    vaccineTypeIds: [],
    isPregnant: undefined,
    pregnancyResult: undefined
  });

  // Track selected drugs and their categories
  const [selectedDrugs, setSelectedDrugs] = useState<Map<number, number[]>>(new Map());

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

  const getSelectedEventTypeName = (): string => {
    if (!eventOptions || !formData.eventTypeId) return '';
    const eventType = eventOptions.eventTypes.find(et => et.id === formData.eventTypeId);
    return eventType?.name || '';
  };

  const shouldShowEndDate = (): boolean => {
    const eventTypeName = getSelectedEventTypeName().toLowerCase();
    
    // Event types that should NOT show end date (instant/single-point events)
    const instantEventKeywords = [
      'examination', 'vyšetření', 'kontrola', 'prohlídka',
      'vaccine', 'očkování', 'vakcína',
      'injury', 'úraz', 'zranění',
      'návštěva', 'visit'
    ];
    
    return !instantEventKeywords.some(keyword => eventTypeName.includes(keyword));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.eventTypeId) {
      return;
    }

    try {
      setLoading(true);
      
      // Convert selectedDrugs Map to drugUses array
      const drugUses: DrugUseRequest[] = Array.from(selectedDrugs.entries()).map(([drugId, categoryIds]) => ({
        drugId,
        categoryIds
      }));

      // Determine happenedTo based on event type
      let happenedTo: string | undefined;
      if (!shouldShowEndDate()) {
        // For instant events, set end time same as start time
        happenedTo = formData.happenedAt;
      } else {
        // For duration events, use the provided end time or undefined if empty
        happenedTo = formData.happenedTo && formData.happenedTo.trim() !== '' ? formData.happenedTo : undefined;
      }

      const submitData = {
        ...formData,
        happenedTo,
        drugUses
      };

      await apiClient.post('/api/events', submitData);
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
      drugUses: [],
      examinationTypeIds: [],
      symptomIds: [],
      injuryTypeIds: [],
      vaccineTypeIds: [],
      isPregnant: undefined,
      pregnancyResult: undefined
    });
    setSelectedDrugs(new Map());
  };

  const handleDrugToggle = (drugId: number, checked: boolean) => {
    const newSelectedDrugs = new Map(selectedDrugs);
    if (checked) {
      newSelectedDrugs.set(drugId, []);
    } else {
      newSelectedDrugs.delete(drugId);
    }
    setSelectedDrugs(newSelectedDrugs);
  };

  const handleDrugCategoryToggle = (drugId: number, categoryId: number, checked: boolean) => {
    const newSelectedDrugs = new Map(selectedDrugs);
    const currentCategories = newSelectedDrugs.get(drugId) || [];
    
    if (checked) {
      newSelectedDrugs.set(drugId, [...currentCategories, categoryId]);
    } else {
      newSelectedDrugs.set(drugId, currentCategories.filter(id => id !== categoryId));
    }
    
    setSelectedDrugs(newSelectedDrugs);
  };

  const handleCheckboxChange = (
    field: 'examinationTypeIds' | 'symptomIds' | 'injuryTypeIds' | 'vaccineTypeIds',
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

  const renderEventTypeSpecificForm = () => {
    if (!formData.eventTypeId || !eventOptions) return null;

    const eventTypeName = getSelectedEventTypeName().toLowerCase();
    
    // Map event type names to their corresponding forms
    // This works with any event type name containing these keywords
    const showDrugs = eventTypeName.includes('drug') || eventTypeName.includes('lék') || eventTypeName.includes('léčba');
    const showExaminations = eventTypeName.includes('examination') || eventTypeName.includes('vyšetření') || eventTypeName.includes('kontrola');
    const showSymptoms = eventTypeName.includes('symptom') || eventTypeName.includes('příznak');
    const showInjuries = eventTypeName.includes('injury') || eventTypeName.includes('úraz') || eventTypeName.includes('zranění');
    const showVaccines = eventTypeName.includes('vaccine') || eventTypeName.includes('očkování') || eventTypeName.includes('vakcína');
    const showPregnancy = eventTypeName.includes('pregnancy') || eventTypeName.includes('těhotenství');
    
    // If no specific match, show all options (for general event types like "Návštěva", "Operace")
    const showAll = !showDrugs && !showExaminations && !showSymptoms && !showInjuries && !showVaccines && !showPregnancy;

    return (
      <>
        {(showDrugs || showAll) && renderDrugUseForm()}
        {(showExaminations || showAll) && renderExaminationForm()}
        {(showSymptoms || showAll) && renderSymptomsForm()}
        {(showInjuries || showAll) && renderInjuryForm()}
        {(showVaccines || showAll) && renderVaccineForm()}
        {(showPregnancy || showAll) && renderPregnancyForm()}
      </>
    );
  };

  const renderDrugUseForm = () => {
    if (!eventOptions?.drugs.length) return null;

    return (
      <div className="form-section">
        <h3>{t('events.drugs')}</h3>
        <p className="form-help-text">{t('events.drugUseHelp')}</p>
        <div className="drug-selection">
          {eventOptions.drugs.map(drug => (
            <div key={drug.id} className="drug-item">
              <label className="checkbox-item drug-checkbox">
                <input
                  type="checkbox"
                  checked={selectedDrugs.has(drug.id)}
                  onChange={(e) => handleDrugToggle(drug.id, e.target.checked)}
                />
                <span className="drug-name">{drug.name}</span>
              </label>
              
              {selectedDrugs.has(drug.id) && eventOptions.drugCategories.length > 0 && (
                <div className="drug-categories">
                  <p className="category-label">{t('events.selectDrugCategories')}</p>
                  <div className="category-grid">
                    {eventOptions.drugCategories.map(category => (
                      <label key={category.id} className="checkbox-item category-checkbox">
                        <input
                          type="checkbox"
                          checked={(selectedDrugs.get(drug.id) || []).includes(category.id)}
                          onChange={(e) => handleDrugCategoryToggle(drug.id, category.id, e.target.checked)}
                        />
                        <span>{category.name}</span>
                      </label>
                    ))}
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      </div>
    );
  };

  const renderExaminationForm = () => {
    if (!eventOptions?.examinationTypes.length) return null;

    return (
      <div className="form-section">
        <h3>{t('events.examinations')}</h3>
        <p className="form-help-text">{t('events.examinationHelp')}</p>
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
    );
  };

  const renderPregnancyForm = () => {
    return (
      <div className="form-section">
        <h3>{t('events.pregnancy')}</h3>
        <p className="form-help-text">{t('events.pregnancyHelp')}</p>
        <div className="pregnancy-result">
          <label>{t('events.pregnancyResult')} *</label>
          <div className="radio-group">
            <label className="radio-item">
              <input
                type="radio"
                name="pregnancyResult"
                checked={formData.pregnancyResult === true}
                onChange={() => setFormData(prev => ({ ...prev, isPregnant: true, pregnancyResult: true }))}
              />
              <span>{t('events.successful')}</span>
            </label>
            <label className="radio-item">
              <input
                type="radio"
                name="pregnancyResult"
                checked={formData.pregnancyResult === false}
                onChange={() => setFormData(prev => ({ ...prev, isPregnant: true, pregnancyResult: false }))}
              />
              <span>{t('events.unsuccessful')}</span>
            </label>
          </div>
        </div>
      </div>
    );
  };

  const renderInjuryForm = () => {
    if (!eventOptions?.injuryTypes.length) return null;

    return (
      <div className="form-section">
        <h3>{t('events.injuries')}</h3>
        <p className="form-help-text">{t('events.injuryHelp')}</p>
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
    );
  };

  const renderVaccineForm = () => {
    if (!eventOptions?.vaccineTypes.length) return null;

    return (
      <div className="form-section">
        <h3>{t('events.vaccines')}</h3>
        <p className="form-help-text">{t('events.vaccineHelp')}</p>
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
    );
  };

  const renderSymptomsForm = () => {
    if (!eventOptions?.symptoms.length) return null;

    return (
      <div className="form-section">
        <h3>{t('events.symptoms')}</h3>
        <p className="form-help-text">{t('events.symptomsHelp')}</p>
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
    );
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
                  onChange={(e) => {
                    const newEventTypeId = parseInt(e.target.value);
                    setFormData(prev => ({ 
                      ...prev, 
                      eventTypeId: newEventTypeId,
                      // Reset event-specific data when changing type
                      examinationTypeIds: [],
                      symptomIds: [],
                      injuryTypeIds: [],
                      vaccineTypeIds: [],
                      isPregnant: undefined,
                      pregnancyResult: undefined
                    }));
                    setSelectedDrugs(new Map());
                  }}
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

              {shouldShowEndDate() && (
                <div className="form-group">
                  <label htmlFor="happenedTo">
                    {t('events.happenedTo')}
                    <span className="optional-hint"> ({t('events.leaveEmptyIfOngoing')})</span>
                  </label>
                  <input
                    type="datetime-local"
                    id="happenedTo"
                    value={formData.happenedTo}
                    onChange={(e) => setFormData(prev => ({ ...prev, happenedTo: e.target.value }))}
                  />
                </div>
              )}
            </div>

            {/* Event-type-specific form */}
            {formData.eventTypeId > 0 && renderEventTypeSpecificForm()}

            {/* Comment section - available for all event types */}
            {formData.eventTypeId > 0 && (
              <div className="form-section">
                <h3>{t('events.comment')}</h3>
                <div className="form-group">
                  <textarea
                    id="comment"
                    value={formData.comment}
                    onChange={(e) => setFormData(prev => ({ ...prev, comment: e.target.value }))}
                    rows={3}
                    placeholder={t('events.commentPlaceholder')}
                  />
                </div>
              </div>
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
