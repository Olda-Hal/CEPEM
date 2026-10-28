import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient, ApiError } from '../../utils/api';
import { useToast } from '../../contexts/ToastContext';
import { BlockReservationSlotRequest, ExaminationType, ReservationSlot } from '../../types';
import { formatDateTime } from '../../utils/format';

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

  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [email, setEmail] = useState('');
  const [examinationTypeId, setExaminationTypeId] = useState('');

  const handleBlockSelectedSlot = async () => {
    if (!selectedSlotId) {
      toast.error(t('reservationSlots.errors.selectAvailableSlot'));
      return;
    }

    if (!firstName.trim() || !lastName.trim() || !examinationTypeId) {
      toast.error(t('reservationSlots.errors.fillBookingData'));
      return;
    }

    const request: BlockReservationSlotRequest = {
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
