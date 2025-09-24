import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../utils/api';
import './CreatePatientModal.css';

interface CreatePatientData {
  firstName: string;
  lastName: string;
  birthDate: string;
  phoneNumber: string;
  email: string;
  insuranceNumber: string;
  gender: string;
  titleBefore: string;
  titleAfter: string;
  uid: string;
}

interface CreatePatientModalProps {
  isOpen: boolean;
  onClose: () => void;
  onPatientCreated: () => void;
}

export const CreatePatientModal: React.FC<CreatePatientModalProps> = ({
  isOpen,
  onClose,
  onPatientCreated
}) => {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  const [patientData, setPatientData] = useState<CreatePatientData>({
    firstName: '',
    lastName: '',
    birthDate: '',
    phoneNumber: '',
    email: '',
    insuranceNumber: '',
    gender: 'M',
    titleBefore: '',
    titleAfter: '',
    uid: ''
  });

  const [errors, setErrors] = useState<Partial<CreatePatientData>>({});

  const handleInputChange = (field: keyof CreatePatientData, value: string) => {
    setPatientData(prev => ({ ...prev, [field]: value }));
    
    // Clear field error when user starts typing
    if (errors[field]) {
      setErrors(prev => ({ ...prev, [field]: undefined }));
    }
  };

  const validateForm = (): boolean => {
    const newErrors: Partial<CreatePatientData> = {};

    if (!patientData.firstName.trim()) {
      newErrors.firstName = t('patients.errors.firstNameRequired');
    }

    if (!patientData.lastName.trim()) {
      newErrors.lastName = t('patients.errors.lastNameRequired');
    }

    if (!patientData.birthDate) {
      newErrors.birthDate = t('patients.errors.birthDateRequired');
    }

    if (!patientData.uid.trim()) {
      newErrors.uid = t('patients.errors.uidRequired');
    } else if (!/^\d{10}$/.test(patientData.uid.replace(/\D/g, ''))) {
      newErrors.uid = t('patients.errors.uidInvalid');
    }

    if (!patientData.insuranceNumber.trim()) {
      newErrors.insuranceNumber = t('patients.errors.insuranceNumberRequired');
    } else if (!/^\d+$/.test(patientData.insuranceNumber)) {
      newErrors.insuranceNumber = t('patients.errors.insuranceNumberInvalid');
    }

    if (patientData.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(patientData.email)) {
      newErrors.email = t('patients.errors.emailInvalid');
    }

    if (patientData.phoneNumber && !/^(\+420)?\s?\d{3}\s?\d{3}\s?\d{3}$/.test(patientData.phoneNumber.replace(/\s/g, ''))) {
      newErrors.phoneNumber = t('patients.errors.phoneInvalid');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!validateForm()) {
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const createData = {
        ...patientData,
        insuranceNumber: parseInt(patientData.insuranceNumber)
      };

      await apiClient.post('/api/patients', createData);
      
      // Reset form
      setPatientData({
        firstName: '',
        lastName: '',
        birthDate: '',
        phoneNumber: '',
        email: '',
        insuranceNumber: '',
        gender: 'M',
        titleBefore: '',
        titleAfter: '',
        uid: ''
      });
      
      onPatientCreated();
      onClose();
    } catch (error: any) {
      console.error('Error creating patient:', error);
      setError(error.response?.data?.message || t('patients.errors.createFailed'));
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    if (!loading) {
      setError(null);
      setErrors({});
      onClose();
    }
  };

  if (!isOpen) return null;

  return (
    <div className="modal-overlay" onClick={handleClose}>
      <div className="modal-content create-patient-modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>{t('patients.createPatient')}</h2>
          <button 
            className="close-button" 
            onClick={handleClose}
            disabled={loading}
          >
            ×
          </button>
        </div>

        <form onSubmit={handleSubmit} className="create-patient-form">
          <div className="form-grid">
            <div className="form-group">
              <label htmlFor="titleBefore">{t('patients.titleBefore')}</label>
              <input
                id="titleBefore"
                type="text"
                value={patientData.titleBefore}
                onChange={(e) => handleInputChange('titleBefore', e.target.value)}
                placeholder={t('patients.titleBeforePlaceholder')}
                disabled={loading}
              />
            </div>

            <div className="form-group required">
              <label htmlFor="firstName">{t('patients.firstName')} *</label>
              <input
                id="firstName"
                type="text"
                value={patientData.firstName}
                onChange={(e) => handleInputChange('firstName', e.target.value)}
                className={errors.firstName ? 'error' : ''}
                disabled={loading}
                required
              />
              {errors.firstName && <span className="error-text">{errors.firstName}</span>}
            </div>

            <div className="form-group required">
              <label htmlFor="lastName">{t('patients.lastName')} *</label>
              <input
                id="lastName"
                type="text"
                value={patientData.lastName}
                onChange={(e) => handleInputChange('lastName', e.target.value)}
                className={errors.lastName ? 'error' : ''}
                disabled={loading}
                required
              />
              {errors.lastName && <span className="error-text">{errors.lastName}</span>}
            </div>

            <div className="form-group">
              <label htmlFor="titleAfter">{t('patients.titleAfter')}</label>
              <input
                id="titleAfter"
                type="text"
                value={patientData.titleAfter}
                onChange={(e) => handleInputChange('titleAfter', e.target.value)}
                placeholder={t('patients.titleAfterPlaceholder')}
                disabled={loading}
              />
            </div>

            <div className="form-group required">
              <label htmlFor="uid">{t('patients.uid')} *</label>
              <input
                id="uid"
                type="text"
                value={patientData.uid}
                onChange={(e) => handleInputChange('uid', e.target.value)}
                className={errors.uid ? 'error' : ''}
                placeholder="1234567890"
                maxLength={10}
                disabled={loading}
                required
              />
              {errors.uid && <span className="error-text">{errors.uid}</span>}
            </div>

            <div className="form-group required">
              <label htmlFor="birthDate">{t('patients.birthDate')} *</label>
              <input
                id="birthDate"
                type="date"
                value={patientData.birthDate}
                onChange={(e) => handleInputChange('birthDate', e.target.value)}
                className={errors.birthDate ? 'error' : ''}
                disabled={loading}
                required
              />
              {errors.birthDate && <span className="error-text">{errors.birthDate}</span>}
            </div>

            <div className="form-group required">
              <label htmlFor="gender">{t('patients.gender')} *</label>
              <select
                id="gender"
                value={patientData.gender}
                onChange={(e) => handleInputChange('gender', e.target.value)}
                disabled={loading}
                required
              >
                <option value="M">{t('patients.male')}</option>
                <option value="F">{t('patients.female')}</option>
              </select>
            </div>

            <div className="form-group required">
              <label htmlFor="insuranceNumber">{t('patients.insuranceNumber')} *</label>
              <input
                id="insuranceNumber"
                type="text"
                value={patientData.insuranceNumber}
                onChange={(e) => handleInputChange('insuranceNumber', e.target.value.replace(/\D/g, ''))}
                className={errors.insuranceNumber ? 'error' : ''}
                placeholder="123456789"
                disabled={loading}
                required
              />
              {errors.insuranceNumber && <span className="error-text">{errors.insuranceNumber}</span>}
            </div>

            <div className="form-group">
              <label htmlFor="phoneNumber">{t('patients.phone')}</label>
              <input
                id="phoneNumber"
                type="tel"
                value={patientData.phoneNumber}
                onChange={(e) => handleInputChange('phoneNumber', e.target.value)}
                className={errors.phoneNumber ? 'error' : ''}
                placeholder="+420 123 456 789"
                disabled={loading}
              />
              {errors.phoneNumber && <span className="error-text">{errors.phoneNumber}</span>}
            </div>

            <div className="form-group">
              <label htmlFor="email">{t('patients.email')}</label>
              <input
                id="email"
                type="email"
                value={patientData.email}
                onChange={(e) => handleInputChange('email', e.target.value)}
                className={errors.email ? 'error' : ''}
                placeholder="patient@example.com"
                disabled={loading}
              />
              {errors.email && <span className="error-text">{errors.email}</span>}
            </div>
          </div>

          {error && (
            <div className="error-message">
              {error}
            </div>
          )}

          <div className="modal-actions">
            <button 
              type="button" 
              onClick={handleClose}
              className="button secondary"
              disabled={loading}
            >
              {t('common.cancel')}
            </button>
            <button 
              type="submit" 
              className="button primary"
              disabled={loading}
            >
              {loading ? t('common.creating') : t('patients.createPatient')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
