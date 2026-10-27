import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient, ApiError } from '../../utils/api';
import { useToast } from '../../contexts/ToastContext';
import {
  CreateReservationSlotItemRequest,
  CreateReservationSlotsRequest,
  CopyReservationSlotsDayRequest,
  ReservationSlotStatus
} from '../../types';

const DEFAULT_DURATION_MINUTES = 30;

/** Builds a UTC ISO string from local date + HH:mm inputs. */
const createLocalDateTime = (dateInput: string, timeInput: string) => {
  const [year, month, day] = dateInput.split('-').map(Number);
  const [hours, minutes] = timeInput.split(':').map(Number);
  return new Date(year, month - 1, day, hours, minutes);
};

interface SlotCreationPanelProps {
  hospitalId: string;
  /** The day currently displayed; the copy-day target and the create-day result navigate to it */
  targetDate: string;
  onDateChange: (date: string) => void;
  onSlotsChanged: () => void | Promise<void>;
}

/** Daily slot generation and copy-day panel for one hospital. */
export const SlotCreationPanel: React.FC<SlotCreationPanelProps> = ({
  hospitalId,
  targetDate,
  onDateChange,
  onSlotsChanged
}) => {
  const { t } = useTranslation();
  const toast = useToast();

  const [slotDate, setSlotDate] = useState(targetDate);
  const [slotStartTime, setSlotStartTime] = useState('08:00');
  const [slotEndTime, setSlotEndTime] = useState('16:00');
  const [slotDurationMinutes, setSlotDurationMinutes] = useState(DEFAULT_DURATION_MINUTES);
  const [slotPublicNote, setSlotPublicNote] = useState('');
  const [slotInternalNote, setSlotInternalNote] = useState('');
  const [slotInitialStatus, setSlotInitialStatus] = useState<ReservationSlotStatus>('AVAILABLE');
  const [copySourceDate, setCopySourceDate] = useState(targetDate);
  const [copyPreserveStatus, setCopyPreserveStatus] = useState(false);

  const handleCreateDailySlots = async () => {
    if (!hospitalId) {
      toast.error(t('reservationSlots.errors.selectHospitalFirst'));
      return;
    }

    if (!slotDate || !slotStartTime || !slotEndTime || Number(slotDurationMinutes) <= 0) {
      toast.error(t('reservationSlots.errors.fillSchedule'));
      return;
    }

    const start = createLocalDateTime(slotDate, slotStartTime);
    const end = createLocalDateTime(slotDate, slotEndTime);

    if (start >= end) {
      toast.error(t('reservationSlots.errors.startBeforeEnd'));
      return;
    }

    const durationMinutes = Number(slotDurationMinutes);
    const slotItems: CreateReservationSlotItemRequest[] = [];
    let currentStart = new Date(start);

    while (currentStart < end) {
      const currentEnd = new Date(currentStart.getTime() + durationMinutes * 60000);
      const boundedEnd = currentEnd > end ? new Date(end) : currentEnd;

      if (boundedEnd <= currentStart) {
        break;
      }

      slotItems.push({
        startDateTime: currentStart.toISOString(),
        endDateTime: boundedEnd.toISOString(),
        publicNote: slotPublicNote.trim() || undefined,
        internalNote: slotInternalNote.trim() || undefined,
        status: slotInitialStatus
      });

      currentStart = boundedEnd;
    }

    if (slotItems.length === 0) {
      toast.error(t('reservationSlots.errors.noGeneratedSlots'));
      return;
    }

    const request: CreateReservationSlotsRequest = {
      hospitalId: Number(hospitalId),
      slots: slotItems
    };

    try {
      await apiClient.post('/api/reservations/slots', request);
      onDateChange(slotDate);
      await onSlotsChanged();
      toast.success(t('reservationSlots.messages.generatedSuccess'));
    } catch (error) {
      const errorMessage = error instanceof ApiError ? error.message : t('reservationSlots.errors.creatingSlots');
      toast.error(errorMessage);
      console.error(t('reservationSlots.errors.creatingSlots'), error);
    }
  };

  const handleCopyDaySlots = async () => {
    if (!hospitalId) {
      toast.error(t('reservationSlots.errors.selectHospitalFirst'));
      return;
    }

    if (!copySourceDate || !targetDate) {
      toast.error(t('reservationSlots.errors.selectCopyDates'));
      return;
    }

    const request: CopyReservationSlotsDayRequest = {
      sourceDate: copySourceDate,
      targetDate: targetDate,
      preserveStatus: copyPreserveStatus
    };

    try {
      await apiClient.post(`/api/reservations/slots/hospital/${hospitalId}/copy-day`, request);
      await onSlotsChanged();
      toast.success(t('reservationSlots.messages.copySuccess'));
    } catch (error) {
      const errorMessage = error instanceof ApiError ? error.message : t('reservationSlots.errors.copyFailed');
      toast.error(errorMessage);
      console.error(t('reservationSlots.errors.copyFailed'), error);
    }
  };

  return (
    <article className="panel form-panel">
      <div className="panel-header">
        <h3>{t('reservationSlots.create.title')}</h3>
        <p>{t('reservationSlots.create.description')}</p>
      </div>

      <div className="form-grid">
        <label>
          <span>{t('reservationSlots.create.date')}</span>
          <input type="date" value={slotDate} onChange={(event) => setSlotDate(event.target.value)} />
        </label>
        <label>
          <span>{t('reservationSlots.create.startTime')}</span>
          <input type="time" value={slotStartTime} onChange={(event) => setSlotStartTime(event.target.value)} />
        </label>
        <label>
          <span>{t('reservationSlots.create.endTime')}</span>
          <input type="time" value={slotEndTime} onChange={(event) => setSlotEndTime(event.target.value)} />
        </label>
        <label>
          <span>{t('reservationSlots.create.duration')}</span>
          <input
            type="number"
            min="1"
            value={slotDurationMinutes}
            onChange={(event) => setSlotDurationMinutes(Number(event.target.value))}
          />
        </label>
        <label className="full-width">
          <span>{t('reservationSlots.create.publicNote')}</span>
          <textarea value={slotPublicNote} onChange={(event) => setSlotPublicNote(event.target.value)} rows={3} />
        </label>
        <label className="full-width">
          <span>{t('reservationSlots.create.internalNote')}</span>
          <textarea value={slotInternalNote} onChange={(event) => setSlotInternalNote(event.target.value)} rows={3} />
        </label>
        <label>
          <span>{t('reservationSlots.create.initialStatus')}</span>
          <select value={slotInitialStatus} onChange={(event) => setSlotInitialStatus(event.target.value as ReservationSlotStatus)}>
            <option value="AVAILABLE">{t('reservationSlots.status.available')}</option>
            <option value="UNAVAILABLE">{t('reservationSlots.status.unavailable')}</option>
          </select>
        </label>
      </div>

      <button className="primary-button" onClick={handleCreateDailySlots} disabled={!hospitalId}>
        {t('reservationSlots.create.generateButton')}
      </button>

      <div className="panel-header" style={{ marginTop: 24 }}>
        <h3>{t('reservationSlots.copy.title')}</h3>
        <p>{t('reservationSlots.copy.description')}</p>
      </div>

      <div className="form-grid">
        <label>
          <span>{t('reservationSlots.copy.sourceDate')}</span>
          <input type="date" value={copySourceDate} onChange={(event) => setCopySourceDate(event.target.value)} />
        </label>
        <label>
          <span>{t('reservationSlots.copy.targetDate')}</span>
          <input type="date" value={targetDate} onChange={(event) => onDateChange(event.target.value)} />
        </label>
        <label className="full-width" style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <input
            type="checkbox"
            checked={copyPreserveStatus}
            onChange={(event) => setCopyPreserveStatus(event.target.checked)}
          />
          <span>{t('reservationSlots.copy.preserveStatus')}</span>
        </label>
      </div>

      <button className="primary-button" onClick={handleCopyDaySlots} disabled={!hospitalId}>
        {t('reservationSlots.copy.button')}
      </button>
    </article>
  );
};

export default SlotCreationPanel;
