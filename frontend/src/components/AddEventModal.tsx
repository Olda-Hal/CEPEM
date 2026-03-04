import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { EventOptions, CreateEventRequest, DrugUseRequest } from '../types';
import { apiClient } from '../utils/api';
import SearchableMultiSelect from './SearchableMultiSelect';
import './AddEventModal.css';
import i18n from '../i18n';

type UploadSource = 'file' | 'scanner';

interface SelectedExaminationFile {
  file: File;
  source: UploadSource;
}

interface CreateEventResponse {
  eventId: number;
  examinationIds: number[];
}

const MAX_FILE_SIZE_MB = 10;
const MAX_FILE_SIZE_BYTES = MAX_FILE_SIZE_MB * 1024 * 1024;

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
  const examinationFileInputRef = React.useRef<HTMLInputElement>(null);
  const [eventOptions, setEventOptions] = useState<EventOptions | null>(null);
  const [loading, setLoading] = useState(false);
  const [isGroupMode, setIsGroupMode] = useState(false);
  const [events, setEvents] = useState<CreateEventRequest[]>([]);
  const [selectedExaminationFiles, setSelectedExaminationFiles] = useState<SelectedExaminationFile[]>([]);
  const [examinationUploadError, setExaminationUploadError] = useState<string | null>(null);
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
  }, [isOpen, i18n.language]);

  const loadEventOptions = async () => {
    try {
      setLoading(true);
      const language = i18n.language;
      const response = await apiClient.get<EventOptions>(`/api/events/options?language=${language}`);
      setEventOptions(response);

      const defaultExaminationEventTypeId = getDefaultExaminationEventTypeId(response);
      if (defaultExaminationEventTypeId > 0) {
        setFormData(prev => ({
          ...prev,
          eventTypeId: prev.eventTypeId > 0 ? prev.eventTypeId : defaultExaminationEventTypeId
        }));
      }
    } catch (error) {
      console.error('Error loading event options:', error);
    } finally {
      setLoading(false);
    }
  };

  const getDefaultExaminationEventTypeId = (options: EventOptions): number => {
    const examinationType = options.eventTypes.find(type => {
      const normalizedName = type.name.toLowerCase();
      return normalizedName.includes('vyšetření') || normalizedName.includes('examination');
    });

    return examinationType?.id ?? 0;
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

  const handleAddToGroup = () => {
    if (!formData.eventTypeId) {
      return;
    }

    // Convert selectedDrugs Map to drugUses array
    const drugUses: DrugUseRequest[] = Array.from(selectedDrugs.entries()).map(([drugId, categoryIds]) => ({
      drugId,
      categoryIds
    }));

    // Determine happenedTo based on event type
    let happenedTo: string | undefined;
    if (!shouldShowEndDate()) {
      happenedTo = formData.happenedAt;
    } else {
      happenedTo = formData.happenedTo && formData.happenedTo.trim() !== '' ? formData.happenedTo : undefined;
    }

    const newEvent = {
      ...formData,
      happenedTo,
      drugUses
    };

    setEvents([...events, newEvent]);
    resetForm();
  };

  const handleRemoveFromGroup = (index: number) => {
    setEvents(events.filter((_, i) => i !== index));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (isGroupMode && events.length === 0) {
      alert(t('events.pleaseAddAtLeastOneEvent') || 'Please add at least one event to the group');
      return;
    }

    if (!isGroupMode && !formData.eventTypeId) {
      return;
    }

    if (isGroupMode && selectedExaminationFiles.length > 0) {
      alert(t('events.groupUploadNotSupported'));
      return;
    }

    try {
      setLoading(true);
      
      if (isGroupMode) {
        // Submit as group
        await apiClient.post('/api/events/group', {
          patientId,
          events
        });
      } else {
        // Single event submission
        // Convert selectedDrugs Map to drugUses array
        const drugUses: DrugUseRequest[] = Array.from(selectedDrugs.entries()).map(([drugId, categoryIds]) => ({
          drugId,
          categoryIds
        }));

        // Determine happenedTo based on event type
        let happenedTo: string | undefined;
        if (!shouldShowEndDate()) {
          happenedTo = formData.happenedAt;
        } else {
          happenedTo = formData.happenedTo && formData.happenedTo.trim() !== '' ? formData.happenedTo : undefined;
        }

        const submitData = {
          ...formData,
          happenedTo,
          drugUses
        };

        const createEventResponse = await apiClient.post<number | CreateEventResponse>('/api/events', submitData);

        if (selectedExaminationFiles.length > 0) {
          const examinationIds = typeof createEventResponse === 'number'
            ? []
            : createEventResponse.examinationIds;

          if (examinationIds.length > 0) {
            for (const examinationId of examinationIds) {
              for (const selectedFile of selectedExaminationFiles) {
                const uploadFormData = new FormData();
                uploadFormData.append('file', selectedFile.file);
                await apiClient.postFormData(`/api/examinations/${examinationId}/documents`, uploadFormData);
              }
            }
          }
        }
      }
      
      onEventAdded();
      onClose();
      resetForm();
      setEvents([]);
      setIsGroupMode(false);
    } catch (error) {
      console.error('Error creating event:', error);
    } finally {
      setLoading(false);
    }
  };

  const resetForm = () => {
    const defaultExaminationEventTypeId = eventOptions ? getDefaultExaminationEventTypeId(eventOptions) : 0;

    setFormData({
      patientId,
      eventTypeId: defaultExaminationEventTypeId,
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
    setSelectedExaminationFiles([]);
    setExaminationUploadError(null);
  };

  const validateExaminationFile = (file: File): string | null => {
    if (file.type !== 'application/pdf') {
      return t('patients.documents.errors.mustBePdf');
    }

    if (file.size > MAX_FILE_SIZE_BYTES) {
      return t('patients.documents.errors.fileTooLarge', { maxSize: MAX_FILE_SIZE_MB });
    }

    return null;
  };

  const addExaminationFiles = (files: FileList | null, source: UploadSource) => {
    if (!files || files.length === 0) {
      return;
    }

    const existingKeys = new Set(
      selectedExaminationFiles.map(item => `${item.file.name}-${item.file.size}-${item.file.lastModified}`)
    );

    const newFiles: SelectedExaminationFile[] = [];

    for (const file of Array.from(files)) {
      const validationError = validateExaminationFile(file);
      if (validationError) {
        setExaminationUploadError(validationError);
        continue;
      }

      const fileKey = `${file.name}-${file.size}-${file.lastModified}`;
      if (!existingKeys.has(fileKey)) {
        existingKeys.add(fileKey);
        newFiles.push({ file, source });
      }
    }

    if (newFiles.length > 0) {
      setSelectedExaminationFiles(prev => [...prev, ...newFiles]);
      setExaminationUploadError(null);
    }
  };

  const handleExaminationFileInputChange = (event: React.ChangeEvent<HTMLInputElement>, source: UploadSource) => {
    addExaminationFiles(event.target.files, source);
    event.target.value = '';
  };

  const handleExaminationSourceClick = (source: UploadSource) => {
    if (!examinationFileInputRef.current) {
      return;
    }

    examinationFileInputRef.current.dataset.source = source;
    examinationFileInputRef.current.click();
  };

  const removeExaminationFile = (index: number) => {
    setSelectedExaminationFiles(prev => prev.filter((_, currentIndex) => currentIndex !== index));
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(2)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
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
    
    const showDrugs = eventTypeName.includes('drug') || eventTypeName.includes('lék') || eventTypeName.includes('léčba');
    const showExaminations = eventTypeName.includes('examination') || eventTypeName.includes('vyšetření') || eventTypeName.includes('kontrola');
    const showSymptoms = eventTypeName.includes('symptom') || eventTypeName.includes('příznak');
    const showInjuries = eventTypeName.includes('injury') || eventTypeName.includes('úraz') || eventTypeName.includes('zranění');
    const showVaccines = eventTypeName.includes('vaccine') || eventTypeName.includes('očkování') || eventTypeName.includes('vakcína');
    const showPregnancy = eventTypeName.includes('pregnancy') || eventTypeName.includes('těhotenství');

    return (
      <>
        {showDrugs && renderDrugUseForm()}
        {showExaminations && renderExaminationForm()}
        {showSymptoms && renderSymptomsForm()}
        {showInjuries && renderInjuryForm()}
        {showVaccines && renderVaccineForm()}
        {showPregnancy && renderPregnancyForm()}
      </>
    );
  };

  const renderDrugUseForm = () => {
    if (!eventOptions?.drugs.length) return null;

    const selectedDrugIds = Array.from(selectedDrugs.keys());

    const handleAddNewDrug = async (name: string) => {
      const response = await apiClient.post<{ id: number; name: string }>('/api/events/drugs', { name });
      setEventOptions(prev => prev ? {
        ...prev,
        drugs: [...prev.drugs, { id: response.id, name: response.name }]
      } : prev);
      return response;
    };

    const handleDrugSelectionChange = (drugIds: number[]) => {
      const newSelectedDrugs = new Map(selectedDrugs);
      
      selectedDrugIds.forEach(oldId => {
        if (!drugIds.includes(oldId)) {
          newSelectedDrugs.delete(oldId);
        }
      });
      
      drugIds.forEach(newId => {
        if (!selectedDrugs.has(newId)) {
          newSelectedDrugs.set(newId, []);
        }
      });
      
      setSelectedDrugs(newSelectedDrugs);
    };

    const handleAddNewCategory = async (name: string) => {
      const response = await apiClient.post<{ id: number; name: string }>('/api/events/drug-categories', { name });
      setEventOptions(prev => prev ? {
        ...prev,
        drugCategories: [...prev.drugCategories, { id: response.id, name: response.name }]
      } : prev);
      return response;
    };

    return (
      <div className="form-section">
        <SearchableMultiSelect
          label={t('events.drugs')}
          helpText={t('events.drugUseHelp')}
          options={eventOptions.drugs}
          selectedIds={selectedDrugIds}
          onChange={handleDrugSelectionChange}
          placeholder={t('events.searchDrugs')}
          onAddNew={handleAddNewDrug}
        />
        
        {selectedDrugIds.length > 0 && eventOptions.drugCategories.length > 0 && (
          <div className="drug-categories-section">
            <p className="category-section-label">{t('events.selectDrugCategories')}</p>
            {selectedDrugIds.map(drugId => {
              const drug = eventOptions.drugs.find(d => d.id === drugId);
              if (!drug) return null;
              
              return (
                <div key={drugId} className="drug-category-item">
                  <h4 className="drug-category-title">{drug.name}</h4>
                  <SearchableMultiSelect
                    options={eventOptions.drugCategories}
                    selectedIds={selectedDrugs.get(drugId) || []}
                    onChange={(categoryIds) => {
                      const newSelectedDrugs = new Map(selectedDrugs);
                      newSelectedDrugs.set(drugId, categoryIds);
                      setSelectedDrugs(newSelectedDrugs);
                    }}
                    placeholder={t('events.searchCategories')}
                    onAddNew={handleAddNewCategory}
                  />
                </div>
              );
            })}
          </div>
        )}
      </div>
    );
  };

  const renderExaminationForm = () => {
    if (!eventOptions?.examinationTypes.length) return null;

    const handleAddNewExamination = async (name: string) => {
      const response = await apiClient.post<{ id: number; name: string }>('/api/events/examination-types', { name });
      setEventOptions(prev => prev ? {
        ...prev,
        examinationTypes: [...prev.examinationTypes, { id: response.id, name: response.name }]
      } : prev);
      return response;
    };

    return (
      <div className="form-section">
        <SearchableMultiSelect
          label={t('events.examinations')}
          helpText={t('events.examinationHelp')}
          options={eventOptions.examinationTypes}
          selectedIds={formData.examinationTypeIds}
          onChange={(selectedIds) => setFormData(prev => ({ ...prev, examinationTypeIds: selectedIds }))}
          placeholder={t('events.searchExaminations')}
          onAddNew={handleAddNewExamination}
        />

        <div className="examination-documents-section">
          <h4 className="examination-documents-title">{t('patients.documents.title')}</h4>
          <p className="examination-documents-help">{t('patients.documents.selectUploadMethod')}</p>
          <div className="examination-upload-actions">
            <button
              type="button"
              className="examination-upload-button"
              onClick={() => handleExaminationSourceClick('scanner')}
            >
              <svg viewBox="0 0 24 24" width="32" height="32" className="upload-icon">
                <path d="M19.8 10.7L4.2 5l-.7 1.9L17.6 12H5c-1.1 0-2 .9-2 2v4c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2v-5.5c0-.8-.5-1.6-1.2-1.8zM19 18H5v-4h14v4zM6 15h2v2H6v-2z"/>
              </svg>
              <span>{t('patients.documents.scanDocument')}</span>
            </button>
            <button
              type="button"
              className="examination-upload-button"
              onClick={() => handleExaminationSourceClick('file')}
            >
              <svg viewBox="0 0 24 24" width="32" height="32" className="upload-icon">
                <path d="M9 16h6v-6h4l-7-7-7 7h4zm-4 2h14v2H5z"/>
              </svg>
              <span>{t('patients.documents.uploadFile')}</span>
            </button>
          </div>

          {examinationUploadError && (
            <p className="examination-upload-error">{examinationUploadError}</p>
          )}

          {selectedExaminationFiles.length > 0 && (
            <div className="selected-examination-files">
              <h5 className="selected-files-title">
                {t('events.examinationDocumentsSelected', { count: selectedExaminationFiles.length })}
              </h5>
              <div className="selected-files-list">
                {selectedExaminationFiles.map((selectedFile, index) => (
                  <div key={`${selectedFile.file.name}-${selectedFile.file.lastModified}-${index}`} className="selected-examination-file-item">
                    <div className="file-icon-container">
                      <svg viewBox="0 0 24 24" width="24" height="24" className="file-icon">
                        <path d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zM6 20V4h7v5h5v11H6z"/>
                      </svg>
                    </div>
                    <div className="file-details">
                      <div className="file-name">{selectedFile.file.name}</div>
                      <div className="file-meta">
                        {formatFileSize(selectedFile.file.size)} · {selectedFile.source === 'scanner' ? t('patients.documents.scannedDocument') : t('patients.documents.uploadedFile')}
                      </div>
                    </div>
                    <button
                      type="button"
                      className="file-remove-button"
                      onClick={() => removeExaminationFile(index)}
                      aria-label={t('common.delete')}
                      title={t('common.delete')}
                    >
                      <svg viewBox="0 0 24 24" width="18" height="18">
                        <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>
                      </svg>
                    </button>
                  </div>
                ))}
              </div>
            </div>
          )}
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

    const handleAddNewInjury = async (name: string) => {
      const response = await apiClient.post<{ id: number; name: string }>('/api/events/injury-types', { name });
      setEventOptions(prev => prev ? {
        ...prev,
        injuryTypes: [...prev.injuryTypes, { id: response.id, name: response.name }]
      } : prev);
      return response;
    };

    return (
      <div className="form-section">
        <SearchableMultiSelect
          label={t('events.injuries')}
          helpText={t('events.injuryHelp')}
          options={eventOptions.injuryTypes}
          selectedIds={formData.injuryTypeIds}
          onChange={(selectedIds) => setFormData(prev => ({ ...prev, injuryTypeIds: selectedIds }))}
          placeholder={t('events.searchInjuries')}
          onAddNew={handleAddNewInjury}
        />
      </div>
    );
  };

  const renderVaccineForm = () => {
    if (!eventOptions?.vaccineTypes.length) return null;

    const handleAddNewVaccine = async (name: string) => {
      const response = await apiClient.post<{ id: number; name: string }>('/api/events/vaccine-types', { name });
      setEventOptions(prev => prev ? {
        ...prev,
        vaccineTypes: [...prev.vaccineTypes, { id: response.id, name: response.name }]
      } : prev);
      return response;
    };

    return (
      <div className="form-section">
        <SearchableMultiSelect
          label={t('events.vaccines')}
          helpText={t('events.vaccineHelp')}
          options={eventOptions.vaccineTypes}
          selectedIds={formData.vaccineTypeIds}
          onChange={(selectedIds) => setFormData(prev => ({ ...prev, vaccineTypeIds: selectedIds }))}
          placeholder={t('events.searchVaccines')}
          onAddNew={handleAddNewVaccine}
        />
      </div>
    );
  };

  const renderSymptomsForm = () => {
    if (!eventOptions?.symptoms.length) return null;

    const handleAddNewSymptom = async (name: string) => {
      const response = await apiClient.post<{ id: number; name: string }>('/api/events/symptoms', { name });
      setEventOptions(prev => prev ? {
        ...prev,
        symptoms: [...prev.symptoms, { id: response.id, name: response.name }]
      } : prev);
      return response;
    };

    return (
      <div className="form-section">
        <SearchableMultiSelect
          label={t('events.symptoms')}
          helpText={t('events.symptomsHelp')}
          options={eventOptions.symptoms}
          selectedIds={formData.symptomIds}
          onChange={(selectedIds) => setFormData(prev => ({ ...prev, symptomIds: selectedIds }))}
          placeholder={t('events.searchSymptoms')}
          onAddNew={handleAddNewSymptom}
        />
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

            {/* Group mode toggle */}
            <div className="form-section">
              <label className="checkbox-item">
                <input
                  type="checkbox"
                  checked={isGroupMode}
                  onChange={(e) => setIsGroupMode(e.target.checked)}
                />
                <span>{t('events.groupModeLabel') || 'Přidat více eventů do skupiny'}</span>
              </label>
              <p className="form-help-text">
                {t('events.groupModeHelp') || 'V režimu skupiny můžete postupně přidat několik různých eventů, které se v UI zobrazí jako jedna věc, ale v databázi budou samostatné.'}
              </p>
            </div>

            {/* Events in group */}
            {isGroupMode && events.length > 0 && (
              <div className="form-section">
                <h3>{t('events.eventsInGroup') || 'Události ve skupině'}</h3>
                <div className="events-list">
                  {events.map((event, index) => {
                    const eventType = eventOptions?.eventTypes.find(et => et.id === event.eventTypeId);
                    return (
                      <div key={index} className="event-item">
                        <span>{eventType?.name || 'Unknown'} - {new Date(event.happenedAt).toLocaleString('cs-CZ')}</span>
                        <button
                          type="button"
                          onClick={() => handleRemoveFromGroup(index)}
                          className="remove-button"
                        >
                          ×
                        </button>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            <div className="modal-actions">
              <button type="button" onClick={onClose} className="cancel-button">
                {t('common.cancel')}
              </button>
              {isGroupMode && (
                <button 
                  type="button" 
                  onClick={handleAddToGroup} 
                  disabled={!formData.eventTypeId}
                  className="add-to-group-button"
                >
                  {t('events.addToGroup') || 'Přidat do skupiny'}
                </button>
              )}
              <button 
                type="submit" 
                disabled={loading || (!isGroupMode && !formData.eventTypeId) || (isGroupMode && events.length === 0)} 
                className="save-button"
              >
                {loading 
                  ? (selectedExaminationFiles.length > 0 ? t('events.uploadingDocuments') : t('events.creating')) 
                  : isGroupMode 
                    ? (t('events.createGroup') || 'Vytvořit skupinu') 
                    : t('events.createEvent')
                }
              </button>
            </div>

            <input
              ref={examinationFileInputRef}
              type="file"
              accept="application/pdf"
              multiple
              style={{ display: 'none' }}
              onChange={(event) => {
                const source = (event.currentTarget.dataset.source as UploadSource | undefined) ?? 'file';
                handleExaminationFileInputChange(event, source);
              }}
            />
          </form>
        )}
      </div>
    </div>
  );
};

export default AddEventModal;
