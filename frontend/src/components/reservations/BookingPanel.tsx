import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient, ApiError } from '../../utils/api';
import { useToast } from '../../contexts/ToastContext';
import {
  BlockReservationSlotRequest,
  ExaminationType,
  Patient,
  PatientSearchResponse,
  ReservationSlot
} from '../../types';

interface BookingPanelProps {
  selectedSlot: ReservationSlot | null;
  selectedSlotId: number | null;
  isSlotAvailable: boolean;
  examinationTypes: ExaminationType[];
  onBookingCleared: () => void;
  onSlotsChanged: () => void | Promise<void>;
}

/** Block/book the selected slot for a walk-in person. */
export const BookingPanel: React.FC<BookingPanelProps> = ({
  selectedSlot,
  selectedSlotId,
  isSlotAvailable,
  examinationTypes,
  onBookingCleared,
  onSlotsChanged
}) => {
  const { t, i18n } = useTranslation();
  const toast = useToast();

  const [mode, setMode] = useState<'existing' | 'new'>('existing');
  const [patientQuery, setPatientQuery] = useState('');
  const [patientResults, setPatientResults] = useState<Patient[]>([]);
  const [searchingPatients, setSearchingPatients] = useState(false);
  const [selectedPatient, setSelectedPatient] = useState<Patient | null>(null);
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [email, setEmail] = useState('');
  const [examinationTypeId, setExaminationTypeId] = useState('');

  useEffect(() => {
    const term = patientQuery.trim();
    if (mode !== 'existing' || term.length < 2) {
      setPatientResults([]);
      return;
    }

    let cancelled = false;
    const timer = window.setTimeout(async () => {
      setSearchingPatients(true);
      try {
        const params = new URLSearchParams({
          page: '0',
          limit: '10',
          search: term,
          sortBy: 'lastName',
          sortOrder: 'asc'
        });
        const response = await apiClient.get<PatientSearchResponse>(
          `/api/patients/search?${params}`
        );
        if (!cancelled) {
          setPatientResults(response.patients);
        }
      } catch (searchError) {
        console.error('Patient search failed', searchError);
        if (!cancelled) {
          setPatientResults([]);
        }
      } finally {
        if (!cancelled) {
          setSearchingPatients(false);
        }
      }
    }, 300);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [mode, patientQuery]);

  const handleBlockSelectedSlot = async () => {
    if (!selectedSlotId) {
      toast.error(t('reservationSlots.errors.selectAvailableSlot'));
      return;
    }

    if (!examinationTypeId) {
      toast.error(t('reservationSlots.errors.fillBookingData'));
      return;
    }

    if (mode === 'existing' && !selectedPatient) {
      toast.error(t('reservationSlots.errors.selectPatient'));
      return;
    }

    if (mode === 'new' && (!firstName.trim() || !lastName.trim())) {
      toast.error(t('reservationSlots.errors.fillBookingData'));
      return;
    }

    const request: BlockReservationSlotRequest =
      mode === 'existing' && selectedPatient
        ? {
            examinationTypeId: Number(examinationTypeId),
            personId: selectedPatient.personId
          }
        : {
            examinationTypeId: Number(examinationTypeId),
            newPerson: {
              firstName: firstName.trim(),
              lastName: lastName.trim(),
              phoneNumber: phoneNumber.trim() || undefined,
              email: email.trim() || undefined
            }
          };

    try {
      await apiClient.post(`/api/reservations/slots/${selectedSlotId}/block`, request);
      setFirstName('');
      setLastName('');
      setPhoneNumber('');
      setEmail('');
      setExaminationTypeId('');
      setPatientQuery('');
      setPatientResults([]);
      setSelectedPatient(null);
      onBookingCleared();
      await onSlotsChanged();
      toast.success(t('reservationSlots.messages.blockedSuccess'));
    } catch (error) {
      const errorMessage =
        error instanceof ApiError ? error.message : t('reservationSlots.errors.blockFailed');
      toast.error(errorMessage);
      console.error(t('reservationSlots.errors.blockFailed'), error);
    }
  };

  const formatPatientBirthDate = (value: string) =>
    new Date(value).toLocaleDateString(i18n.language);

  const formatDate = (value: string) =>
    new Date(value).toLocaleDateString(i18n.language, { day: 'numeric', month: 'numeric' });
  const formatTime = (value: string) =>
    new Date(value).toLocaleTimeString(i18n.language, { hour: '2-digit', minute: '2-digit' });

  return (
    <article className="panel form-panel">
      <div className="panel-header">
        <h3>{t('reservationSlots.request.title')}</h3>
        <p>{t('reservationSlots.request.description')}</p>
      </div>

      <div className="selected-slot-banner">
        {selectedSlot ? (
          <>
            <strong>
              {formatDate(selectedSlot.startDateTime)} · {formatTime(selectedSlot.startDateTime)} -{' '}
              {formatTime(selectedSlot.endDateTime)}
            </strong>
            <span>{selectedSlot.publicNote || t('reservationSlots.noPublicNote')}</span>
          </>
        ) : (
          <span>{t('reservationSlots.request.selectHint')}</span>
        )}
      </div>

      <div
        className="booking-mode-toggle"
        role="group"
        aria-label={t('reservationSlots.request.patientMode')}
      >
        <button
          type="button"
          className={mode === 'existing' ? 'active' : ''}
          onClick={() => {
            setMode('existing');
            setSelectedPatient(null);
            setPatientQuery('');
            setPatientResults([]);
          }}
        >
          {t('reservationSlots.request.existingPatient')}
        </button>
        <button
          type="button"
          className={mode === 'new' ? 'active' : ''}
          onClick={() => {
            setMode('new');
            setSelectedPatient(null);
            setPatientQuery('');
            setPatientResults([]);
          }}
        >
          {t('reservationSlots.request.newPatient')}
        </button>
      </div>

      {mode === 'existing' ? (
        <div className="booking-existing-patient">
          <label className="full-width">
            <span>{t('reservationSlots.request.searchPatient')}</span>
            <input
              value={patientQuery}
              onChange={(event) => setPatientQuery(event.target.value)}
              placeholder={t('reservationSlots.request.searchPatientPlaceholder')}
            />
          </label>

          {searchingPatients && <p className="booking-hint">{t('common.loading')}</p>}
          {!searchingPatients && patientQuery.trim().length >= 2 && patientResults.length === 0 && (
            <p className="booking-hint">{t('reservationSlots.request.noPatients')}</p>
          )}

          {patientResults.length > 0 && (
            <ul className="booking-patient-results">
              {patientResults.map((patient) => (
                <li key={patient.id}>
                  <button
                    type="button"
                    className={selectedPatient?.id === patient.id ? 'selected' : ''}
                    onClick={() => {
                      setSelectedPatient(patient);
                      setPatientQuery('');
                      setPatientResults([]);
                    }}
                  >
                    <strong>{patient.fullName}</strong>
                    <span>
                      UID {patient.uid} · {formatPatientBirthDate(patient.birthDate)}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}

          {selectedPatient && (
            <div className="booking-selected-patient">
              <span>
                {t('reservationSlots.request.selectedPatient')}:{' '}
                <strong>{selectedPatient.fullName}</strong> ({t('patients.fields.uid')}{' '}
                {selectedPatient.uid})
              </span>
              <button type="button" onClick={() => setSelectedPatient(null)}>
                {t('reservationSlots.request.clearSelection')}
              </button>
            </div>
          )}
        </div>
      ) : (
        <div className="form-grid">
          <label>
            <span>{t('reservationSlots.request.firstName')}</span>
            <input value={firstName} onChange={(event) => setFirstName(event.target.value)} />
          </label>
          <label>
            <span>{t('reservationSlots.request.lastName')}</span>
            <input value={lastName} onChange={(event) => setLastName(event.target.value)} />
          </label>
          <label>
            <span>{t('reservationSlots.request.phone')}</span>
            <input value={phoneNumber} onChange={(event) => setPhoneNumber(event.target.value)} />
          </label>
          <label>
            <span>{t('reservationSlots.request.email')}</span>
            <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} />
          </label>
        </div>
      )}

      <div className="form-grid">
        <label className="full-width">
          <span>{t('reservationSlots.request.examinationType')}</span>
          <select
            value={examinationTypeId}
            onChange={(event) => setExaminationTypeId(event.target.value)}
          >
            <option value="">{t('reservationSlots.request.selectExaminationType')}</option>
            {examinationTypes.map((type) => (
              <option key={type.id} value={type.id}>
                {type.name}
              </option>
            ))}
          </select>
        </label>
      </div>

      <button
        className="primary-button"
        onClick={handleBlockSelectedSlot}
        disabled={!selectedSlotId || !isSlotAvailable}
      >
        {t('reservationSlots.request.blockButton')}
      </button>
    </article>
  );
};

export default BookingPanel;
