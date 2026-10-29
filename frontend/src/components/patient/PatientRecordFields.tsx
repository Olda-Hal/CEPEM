import React from 'react';
import { useTranslation } from 'react-i18next';

/** The patient-record field values shared by the create and edit forms. */
export interface PatientRecordFormValues {
  titleBefore: string;
  firstName: string;
  lastName: string;
  titleAfter: string;
  uid: string;
  birthDate: string;
  gender: string;
  insuranceNumber: string;
  email: string;
  phoneNumber: string;
}

export type PatientRecordField = keyof PatientRecordFormValues;

interface PatientRecordFieldsProps {
  values: PatientRecordFormValues;
  onChange: (field: PatientRecordField, value: string) => void;
  disabled?: boolean;
  /** Per-field validation messages (create modal shows them inline) */
  errors?: Partial<Record<PatientRecordField, string>>;
  /** UID regenerate control (create modal only) */
  onRegenerateUid?: () => void;
  generatingUid?: boolean;
}

/**
 * The core patient-record fields (names, titles, UID, birth date, gender,
 * insurance, contact) shared by the create-patient modal and the
 * edit-patient page - previously duplicated with different markup.
 */
export const PatientRecordFields: React.FC<PatientRecordFieldsProps> = ({
  values,
  onChange,
  disabled,
  errors,
  onRegenerateUid,
  generatingUid
}) => {
  const { t } = useTranslation();

  return (
    <div className="patient-record-grid">
      <label className="record-field">
        <span>{t('patients.titleBefore')}</span>
        <input
          type="text"
          value={values.titleBefore}
          onChange={(e) => onChange('titleBefore', e.target.value)}
          placeholder={t('patients.titleBeforePlaceholder')}
          disabled={disabled}
        />
      </label>

      <label className="record-field record-required">
        <span>{t('patients.firstName')} *</span>
        <input
          type="text"
          value={values.firstName}
          onChange={(e) => onChange('firstName', e.target.value)}
          className={errors?.firstName ? 'error' : ''}
          disabled={disabled}
          required
        />
        {errors?.firstName && <span className="error-text">{errors.firstName}</span>}
      </label>

      <label className="record-field record-required">
        <span>{t('patients.lastName')} *</span>
        <input
          type="text"
          value={values.lastName}
          onChange={(e) => onChange('lastName', e.target.value)}
          className={errors?.lastName ? 'error' : ''}
          disabled={disabled}
          required
        />
        {errors?.lastName && <span className="error-text">{errors.lastName}</span>}
      </label>

      <label className="record-field">
        <span>{t('patients.titleAfter')}</span>
        <input
          type="text"
          value={values.titleAfter}
          onChange={(e) => onChange('titleAfter', e.target.value)}
          placeholder={t('patients.titleAfterPlaceholder')}
          disabled={disabled}
        />
      </label>

      <label className="record-field record-required uid-field">
        <span>{t('patients.uid')} *</span>
        <div className="uid-input-group">
          <input
            type="text"
            value={values.uid}
            onChange={(e) => onChange('uid', e.target.value)}
            className={errors?.uid ? 'error' : ''}
            placeholder="1234567890"
            maxLength={10}
            disabled={disabled || generatingUid}
            required
          />
          {onRegenerateUid && (
            <button
              type="button"
              className="regenerate-uid-button"
              onClick={onRegenerateUid}
              disabled={disabled || generatingUid}
              title={t('patients.regenerateUid')}
            >
              {generatingUid ? '⏳' : '🔄'}
            </button>
          )}
        </div>
        {errors?.uid && <span className="error-text">{errors.uid}</span>}
      </label>

      <label className="record-field record-required">
        <span>{t('patients.birthDate')} *</span>
        <input
          type="date"
          value={values.birthDate}
          onChange={(e) => onChange('birthDate', e.target.value)}
          className={errors?.birthDate ? 'error' : ''}
          disabled={disabled}
          required
        />
        {errors?.birthDate && <span className="error-text">{errors.birthDate}</span>}
      </label>

      <label className="record-field record-required">
        <span>{t('patients.gender')} *</span>
        <select
          value={values.gender}
          onChange={(e) => onChange('gender', e.target.value)}
          disabled={disabled}
          required
        >
          <option value="M">{t('patients.male')}</option>
          <option value="F">{t('patients.female')}</option>
        </select>
      </label>

      <label className="record-field record-required">
        <span>{t('patients.insuranceNumber')} *</span>
        <input
          type="number"
          value={values.insuranceNumber}
          onChange={(e) => onChange('insuranceNumber', e.target.value)}
          className={errors?.insuranceNumber ? 'error' : ''}
          disabled={disabled}
          required
        />
        {errors?.insuranceNumber && <span className="error-text">{errors.insuranceNumber}</span>}
      </label>

      <label className="record-field">
        <span>{t('patients.email')}</span>
        <input
          type="email"
          value={values.email}
          onChange={(e) => onChange('email', e.target.value)}
          className={errors?.email ? 'error' : ''}
          disabled={disabled}
        />
        {errors?.email && <span className="error-text">{errors.email}</span>}
      </label>

      <label className="record-field">
        <span>{t('patients.phone')}</span>
        <input
          type="tel"
          value={values.phoneNumber}
          onChange={(e) => onChange('phoneNumber', e.target.value)}
          className={errors?.phoneNumber ? 'error' : ''}
          disabled={disabled}
        />
        {errors?.phoneNumber && <span className="error-text">{errors.phoneNumber}</span>}
      </label>
    </div>
  );
};

export default PatientRecordFields;
