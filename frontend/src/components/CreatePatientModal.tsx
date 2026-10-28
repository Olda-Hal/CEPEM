import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient, ApiError } from '../utils/api';
import PatientRecordFields from './patient/PatientRecordFields';
import BaseModal from './BaseModal';
import './patient/PatientRecordFields.css';
import { CameraCapture } from './CameraCapture';
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
  onPatientCreated: (patientId?: number) => void;
}

export const CreatePatientModal: React.FC<CreatePatientModalProps> = ({
  isOpen,
  onClose,
  onPatientCreated
}) => {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [generatingUid, setGeneratingUid] = useState(false);
  const [showCamera, setShowCamera] = useState(false);
  const [capturedPhoto, setCapturedPhoto] = useState<Blob | null>(null);
  const [photoPreview, setPhotoPreview] = useState<string | null>(null);

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

  // Generate unique UID when modal opens
  React.useEffect(() => {
    if (isOpen && !patientData.uid) {
      generateUniqueUid();
    }
  }, [isOpen]);

  const generateRandomUid = (): string => {
    // Generate 10-digit UID in format YYYYMMXXXX where YYYY is year, MM is month, XXXX is random
    const now = new Date();
    const year = now.getFullYear().toString();
    const month = (now.getMonth() + 1).toString().padStart(2, '0');
    const randomPart = Math.floor(Math.random() * 10000)
      .toString()
      .padStart(4, '0');
    return year + month + randomPart;
  };

  const checkUidExists = async (uid: string): Promise<boolean> => {
    try {
      // Search for patients with this UID
      const response = await apiClient.get(`/api/patients/search?search=${uid}&limit=1`);
      return (response as any)?.patients && (response as any).patients.length > 0;
    } catch (error) {
      console.error('Error checking UID:', error);
      return false; // If we can't check, assume it doesn't exist
    }
  };

  const generateUniqueUid = async () => {
    setGeneratingUid(true);
    let attempts = 0;
    const maxAttempts = 10;

    while (attempts < maxAttempts) {
      const newUid = generateRandomUid();
      const exists = await checkUidExists(newUid);

      if (!exists) {
        setPatientData((prev) => ({ ...prev, uid: newUid }));
        setGeneratingUid(false);
        return;
      }

      attempts++;
    }

    // If we can't find a unique UID after max attempts, generate a longer one
    const fallbackUid = Date.now().toString().slice(-10);
    setPatientData((prev) => ({ ...prev, uid: fallbackUid }));
    setGeneratingUid(false);
  };

  const handleInputChange = (field: keyof CreatePatientData, value: string) => {
    setPatientData((prev) => ({ ...prev, [field]: value }));

    // Clear field error when user starts typing
    if (errors[field]) {
      setErrors((prev) => ({ ...prev, [field]: undefined }));
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

    if (
      patientData.phoneNumber &&
      !/^(\+420)?\s?\d{3}\s?\d{3}\s?\d{3}$/.test(patientData.phoneNumber.replace(/\s/g, ''))
    ) {
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
        phoneNumber: patientData.phoneNumber || undefined,
        email: patientData.email || undefined,
        insuranceNumber: patientData.insuranceNumber
          ? parseInt(patientData.insuranceNumber)
          : undefined
      };

      const response = await apiClient.post<any>('/api/patients', createData);

      if (capturedPhoto && response?.id) {
        try {
          const formData = new FormData();
          formData.append('photo', capturedPhoto, 'patient-photo.jpg');

          await apiClient.postFormData(`/api/patients/${response.id}/photo`, formData);
        } catch (photoError) {
          console.error('Error uploading photo:', photoError);
        }
      }

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
      setCapturedPhoto(null);
      setPhotoPreview(null);

      // Generate new UID for next patient
      setTimeout(() => generateUniqueUid(), 100);

      onPatientCreated(response?.id);
      onClose();
    } catch (error) {
      console.error('Error creating patient:', error);
      setError(error instanceof ApiError ? error.message : t('patients.errors.createFailed'));
    } finally {
      setLoading(false);
    }
  };

  const handlePhotoCapture = (imageData: Blob) => {
    setCapturedPhoto(imageData);
    const reader = new FileReader();
    reader.onloadend = () => {
      setPhotoPreview(reader.result as string);
    };
    reader.readAsDataURL(imageData);
    setShowCamera(false);
  };

  const handleRemovePhoto = () => {
    setCapturedPhoto(null);
    setPhotoPreview(null);
  };

  const handleClose = () => {
    if (!loading && !showCamera) {
      setError(null);
      setErrors({});
      onClose();
    }
  };

  if (!isOpen) return null;

  return (
    <>
      <BaseModal
        title={t('patients.createPatient')}
        onClose={handleClose}
        contentClassName="create-patient-modal"
      >
        <form onSubmit={handleSubmit} className="create-patient-form">
          <div className="form-row photo-section">
            <div className="form-group photo-group">
              <label>{t('patients.photo')}</label>
              {photoPreview ? (
                <div className="photo-preview-container">
                  <img src={photoPreview} alt="Patient" className="photo-preview" />
                  <button
                    type="button"
                    className="button secondary small"
                    onClick={handleRemovePhoto}
                    disabled={loading}
                  >
                    {t('patients.removePhoto')}
                  </button>
                </div>
              ) : (
                <button
                  type="button"
                  className="button secondary"
                  onClick={() => setShowCamera(true)}
                  disabled={loading}
                >
                  📷 {t('patients.takePhoto')}
                </button>
              )}
            </div>
          </div>

          <PatientRecordFields
            values={patientData}
            onChange={(field, value) =>
              handleInputChange(
                field,
                field === 'insuranceNumber' ? value.replace(/\D/g, '') : value
              )
            }
            disabled={loading}
            errors={errors}
            onRegenerateUid={generateUniqueUid}
            generatingUid={generatingUid}
          />

          {error && <div className="error-text form-error">{error}</div>}

          <div className="form-actions">
            <button type="submit" className="button primary" disabled={loading}>
              {loading ? t('common.creating') : t('patients.createPatient')}
            </button>
          </div>
        </form>
      </BaseModal>
    </>
  );
};

export default CreatePatientModal;
