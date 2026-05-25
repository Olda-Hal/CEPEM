import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AppHeader } from '../components/AppHeader';
import { useAuth } from '../contexts/AuthContext';
import { apiClient } from '../utils/api';
import { hasRole } from '../utils/roles';
import {
  BlockReservationSlotRequest,
  ConfirmReservationSlotRequest,
  CopyReservationSlotsDayRequest,
  CreateReservationSlotsRequest,
  CreateReservationSlotItemRequest,
  ExaminationType,
  Hospital,
  ReservationSlot,
  UpdateReservationSlotRequest,
  ReservationSlotStatus
} from '../types';
import './ReservationSlotsPage.css';

const DEFAULT_DURATION_MINUTES = 20;

const formatDateInput = (date: Date) => date.toISOString().slice(0, 10);

const formatDate = (value: string) =>
  new Intl.DateTimeFormat('cs-CZ', { dateStyle: 'medium' }).format(new Date(value));

const formatTime = (value: string) =>
  new Intl.DateTimeFormat('cs-CZ', { hour: '2-digit', minute: '2-digit' }).format(new Date(value));

const createLocalDateTime = (dateValue: string, timeValue: string) => new Date(`${dateValue}T${timeValue}:00`);

export const ReservationSlotsPage: React.FC = () => {
  const { user } = useAuth();
  const { t, i18n } = useTranslation();

  const [hospitals, setHospitals] = useState<Hospital[]>([]);
  const [selectedHospitalId, setSelectedHospitalId] = useState('');
  const [selectedDate, setSelectedDate] = useState(formatDateInput(new Date()));
  const [slots, setSlots] = useState<ReservationSlot[]>([]);
  const [loadingSlots, setLoadingSlots] = useState(false);
  const [examinationTypes, setExaminationTypes] = useState<ExaminationType[]>([]);
  const [selectedSlotId, setSelectedSlotId] = useState<number | null>(null);

  const [slotDate, setSlotDate] = useState(formatDateInput(new Date()));
  const [slotStartTime, setSlotStartTime] = useState('08:00');
  const [slotEndTime, setSlotEndTime] = useState('16:00');
  const [slotDurationMinutes, setSlotDurationMinutes] = useState(DEFAULT_DURATION_MINUTES);
  const [slotPublicNote, setSlotPublicNote] = useState('');
  const [slotInternalNote, setSlotInternalNote] = useState('');
  const [slotInitialStatus, setSlotInitialStatus] = useState<ReservationSlotStatus>('AVAILABLE');

  const [copySourceDate, setCopySourceDate] = useState(formatDateInput(new Date()));
  const [copyPreserveStatus, setCopyPreserveStatus] = useState(false);

  const [bookingFirstName, setBookingFirstName] = useState('');
  const [bookingLastName, setBookingLastName] = useState('');
  const [bookingPhoneNumber, setBookingPhoneNumber] = useState('');
  const [bookingEmail, setBookingEmail] = useState('');
  const [bookingExaminationTypeId, setBookingExaminationTypeId] = useState('');

  const [editPublicNote, setEditPublicNote] = useState('');
  const [editInternalNote, setEditInternalNote] = useState('');
  const [editStatus, setEditStatus] = useState<ReservationSlotStatus>('AVAILABLE');

  const canReviewSlots = hasRole(user, 'Doctor') || hasRole(user, 'SysAdmin') || hasRole(user, 'Admin');
  const canCreateSlots = hasRole(user, 'SysAdmin') || hasRole(user, 'Admin');

  useEffect(() => {
    const loadInitialData = async () => {
      try {
        const hospitalsResponse = await apiClient.get<Hospital[]>('/api/hospitals');
        setHospitals(hospitalsResponse);
      } catch (error) {
        console.error(t('errors.loadingHospitals'), error);
      }

      try {
        const examinationTypesResponse = await apiClient.get<ExaminationType[]>(`/api/examinationtypes?language=${i18n.language}`);
        setExaminationTypes(examinationTypesResponse);
      } catch (error) {
        console.error(t('errors.loadingExaminationTypes'), error);
      }
    };

    loadInitialData();
  }, [i18n.language, t]);

  useEffect(() => {
    if (!selectedHospitalId && hospitals.length > 0) {
      setSelectedHospitalId(String(hospitals[0].id));
    }
  }, [hospitals, selectedHospitalId]);

  const dayStats = useMemo(() => {
    const counts = slots.reduce(
      (accumulator, slot) => {
        accumulator[slot.status] += 1;
        return accumulator;
      },
      {
        UNAVAILABLE: 0,
        AVAILABLE: 0,
        BLOCKED: 0,
        RESERVED: 0
      } as Record<ReservationSlotStatus, number>
    );

    return {
      total: slots.length,
      ...counts
    };
  }, [slots]);

  const sortedSlots = useMemo(
    () => [...slots].sort((left, right) => left.startDateTime.localeCompare(right.startDateTime)),
    [slots]
  );

  const selectedSlot = useMemo(
    () => sortedSlots.find(slot => slot.id === selectedSlotId) ?? null,
    [sortedSlots, selectedSlotId]
  );

  useEffect(() => {
    if (!selectedSlot) {
      setEditPublicNote('');
      setEditInternalNote('');
      setEditStatus('AVAILABLE');
      return;
    }

    setEditPublicNote(selectedSlot.publicNote ?? '');
    setEditInternalNote(selectedSlot.internalNote ?? '');
    setEditStatus(selectedSlot.status);
  }, [selectedSlot]);

  const availableSlots = useMemo(
    () => sortedSlots.filter(slot => slot.status === 'AVAILABLE'),
    [sortedSlots]
  );

  const loadSlots = useCallback(async (hospitalId: string, day: string) => {
    try {
      setLoadingSlots(true);
      const from = createLocalDateTime(day, '00:00');
      const to = new Date(from);
      to.setDate(to.getDate() + 1);

      const response = await apiClient.get<ReservationSlot[]>(
        `/api/reservations/slots/hospital/${hospitalId}?from=${encodeURIComponent(from.toISOString())}&to=${encodeURIComponent(to.toISOString())}`
      );
      setSlots(response);
      setSelectedSlotId(current => (current && response.some(slot => slot.id === current) ? current : null));
    } catch (error) {
      console.error(t('reservationSlots.errors.loadingSlots'), error);
      setSlots([]);
    } finally {
      setLoadingSlots(false);
    }
  }, [t]);

  useEffect(() => {
    if (!selectedHospitalId) {
      setSlots([]);
      return;
    }

    void loadSlots(selectedHospitalId, selectedDate);
  }, [loadSlots, selectedHospitalId, selectedDate]);

  const refreshSlots = async () => {
    if (!selectedHospitalId) return;
    await loadSlots(selectedHospitalId, selectedDate);
  };

  const handleCreateDailySlots = async () => {
    if (!selectedHospitalId) {
      alert(t('reservationSlots.errors.selectHospitalFirst'));
      return;
    }

    if (!slotDate || !slotStartTime || !slotEndTime || Number(slotDurationMinutes) <= 0) {
      alert(t('reservationSlots.errors.fillSchedule'));
      return;
    }

    const start = createLocalDateTime(slotDate, slotStartTime);
    const end = createLocalDateTime(slotDate, slotEndTime);

    if (start >= end) {
      alert(t('reservationSlots.errors.startBeforeEnd'));
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
      alert(t('reservationSlots.errors.noGeneratedSlots'));
      return;
    }

    const request: CreateReservationSlotsRequest = {
      hospitalId: Number(selectedHospitalId),
      slots: slotItems
    };

    try {
      await apiClient.post('/api/reservations/slots', request);
      setSelectedDate(slotDate);
      setSelectedSlotId(null);
      await refreshSlots();
      alert(t('reservationSlots.messages.generatedSuccess'));
    } catch (error: any) {
      const errorMessage = error.response?.data || error.message || t('reservationSlots.errors.creatingSlots');
      alert(errorMessage);
      console.error(t('reservationSlots.errors.creatingSlots'), error);
    }
  };

  const handleCopyDaySlots = async () => {
    if (!selectedHospitalId) {
      alert(t('reservationSlots.errors.selectHospitalFirst'));
      return;
    }

    if (!copySourceDate || !selectedDate) {
      alert(t('reservationSlots.errors.selectCopyDates'));
      return;
    }

    const request: CopyReservationSlotsDayRequest = {
      sourceDate: copySourceDate,
      targetDate: selectedDate,
      preserveStatus: copyPreserveStatus
    };

    try {
      await apiClient.post(`/api/reservations/slots/hospital/${selectedHospitalId}/copy-day`, request);
      await refreshSlots();
      alert(t('reservationSlots.messages.copySuccess'));
    } catch (error: any) {
      const errorMessage = error.response?.data || error.message || t('reservationSlots.errors.copyFailed');
      alert(errorMessage);
      console.error(t('reservationSlots.errors.copyFailed'), error);
    }
  };

  const handleSelectSlot = (slot: ReservationSlot) => {
    setSelectedSlotId(slot.id);
    setBookingExaminationTypeId(slot.examinationTypeId ? String(slot.examinationTypeId) : '');
  };

  const handleEditSlot = (slot: ReservationSlot) => {
    setSelectedSlotId(slot.id);
  };

  const handleUpdateSelectedSlot = async () => {
    if (!selectedSlotId) {
      return;
    }

    const request: UpdateReservationSlotRequest = {
      publicNote: editPublicNote,
      internalNote: editInternalNote,
      status: editStatus
    };

    try {
      await apiClient.put(`/api/reservations/slots/${selectedSlotId}`, request);
      await refreshSlots();
      alert(t('reservationSlots.messages.updatedSuccess'));
    } catch (error: any) {
      const errorMessage = error.response?.data || error.message || t('reservationSlots.errors.updateFailed');
      alert(errorMessage);
      console.error(t('reservationSlots.errors.updateFailed'), error);
    }
  };

  const handleReleaseSelectedSlot = async () => {
    if (!selectedSlotId) {
      return;
    }

    try {
      await apiClient.post(`/api/reservations/slots/${selectedSlotId}/release`);
      await refreshSlots();
      alert(t('reservationSlots.messages.releasedSuccess'));
    } catch (error: any) {
      const errorMessage = error.response?.data || error.message || t('reservationSlots.errors.releaseFailed');
      alert(errorMessage);
      console.error(t('reservationSlots.errors.releaseFailed'), error);
    }
  };

  const handleDeleteSelectedSlot = async () => {
    if (!selectedSlotId) {
      return;
    }

    if (!window.confirm(t('reservationSlots.messages.confirmDeleteSlot'))) {
      return;
    }

    try {
      await apiClient.delete(`/api/reservations/slots/${selectedSlotId}`);
      setSelectedSlotId(null);
      setBookingExaminationTypeId('');
      await refreshSlots();
      alert(t('reservationSlots.messages.deletedSuccess'));
    } catch (error: any) {
      const errorMessage = error.response?.data || error.message || t('reservationSlots.errors.deleteFailed');
      alert(errorMessage);
      console.error(t('reservationSlots.errors.deleteFailed'), error);
    }
  };

  const handleBlockSelectedSlot = async () => {
    if (!selectedSlotId) {
      alert(t('reservationSlots.errors.selectAvailableSlot'));
      return;
    }

    if (!bookingFirstName.trim() || !bookingLastName.trim() || !bookingExaminationTypeId) {
      alert(t('reservationSlots.errors.fillBookingData'));
      return;
    }

    const request: BlockReservationSlotRequest = {
      examinationTypeId: Number(bookingExaminationTypeId),
      newPerson: {
        firstName: bookingFirstName.trim(),
        lastName: bookingLastName.trim(),
        phoneNumber: bookingPhoneNumber.trim() || undefined,
        email: bookingEmail.trim() || undefined
      }
    };

    try {
      await apiClient.post(`/api/reservations/slots/${selectedSlotId}/block`, request);
      setBookingFirstName('');
      setBookingLastName('');
      setBookingPhoneNumber('');
      setBookingEmail('');
      setBookingExaminationTypeId('');
      setSelectedSlotId(null);
      await refreshSlots();
      alert(t('reservationSlots.messages.blockedSuccess'));
    } catch (error: any) {
      const errorMessage = error.response?.data || error.message || t('reservationSlots.errors.blockFailed');
      alert(errorMessage);
      console.error(t('reservationSlots.errors.blockFailed'), error);
    }
  };

  const handleConfirmSlot = async (slotId: number) => {
    if (!user?.id) {
      alert(t('errors.userNotFound'));
      return;
    }

    const request: ConfirmReservationSlotRequest = {
      doctorId: user.id
    };

    try {
      await apiClient.post(`/api/reservations/slots/${slotId}/confirm`, request);
      await refreshSlots();
      alert(t('reservationSlots.messages.confirmedSuccess'));
    } catch (error: any) {
      const errorMessage = error.response?.data || error.message || t('reservationSlots.errors.confirmFailed');
      alert(errorMessage);
      console.error(t('reservationSlots.errors.confirmFailed'), error);
    }
  };

  const handleRejectSlot = async (slotId: number) => {
    try {
      await apiClient.post(`/api/reservations/slots/${slotId}/reject`);
      await refreshSlots();
      alert(t('reservationSlots.messages.rejectedSuccess'));
    } catch (error: any) {
      const errorMessage = error.response?.data || error.message || t('reservationSlots.errors.rejectFailed');
      alert(errorMessage);
      console.error(t('reservationSlots.errors.rejectFailed'), error);
    }
  };

  return (
    <div className="reservation-slots-page">
      <AppHeader sectionTitle={t('reservationSlots.title')} />

      <main className="reservation-slots-layout">
        <section className="hero-panel">
          <div>
            <p className="eyebrow">{t('reservationSlots.eyebrow')}</p>
            <h2>{t('reservationSlots.heroTitle')}</h2>
            <p className="hero-copy">{t('reservationSlots.heroDescription')}</p>
          </div>

          <div className="hero-controls">
            <label>
              <span>{t('reservationSlots.hospital')}</span>
              <select value={selectedHospitalId} onChange={(event) => setSelectedHospitalId(event.target.value)}>
                <option value="">{t('reservationSlots.selectHospital')}</option>
                {hospitals.map(hospital => (
                  <option key={hospital.id} value={hospital.id}>
                    {hospital.name || t('reservationSlots.fallbackHospital', { id: hospital.id })}
                  </option>
                ))}
              </select>
            </label>

            <label>
              <span>{t('reservationSlots.viewDay')}</span>
              <input type="date" value={selectedDate} onChange={(event) => setSelectedDate(event.target.value)} />
            </label>
          </div>
        </section>

        <section className="stats-grid">
          <article className="stat-card">
            <span>{t('reservationSlots.stats.total')}</span>
            <strong>{dayStats.total}</strong>
          </article>
          <article className="stat-card available">
            <span>{t('reservationSlots.stats.available')}</span>
            <strong>{dayStats.AVAILABLE}</strong>
          </article>
          <article className="stat-card blocked">
            <span>{t('reservationSlots.stats.blocked')}</span>
            <strong>{dayStats.BLOCKED}</strong>
          </article>
          <article className="stat-card reserved">
            <span>{t('reservationSlots.stats.reserved')}</span>
            <strong>{dayStats.RESERVED}</strong>
          </article>
        </section>

        <section className="content-grid">
          {canCreateSlots && (
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

              <button className="primary-button" onClick={handleCreateDailySlots} disabled={!selectedHospitalId}>
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
                  <input type="date" value={selectedDate} onChange={(event) => setSelectedDate(event.target.value)} />
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

              <button className="primary-button" onClick={handleCopyDaySlots} disabled={!selectedHospitalId}>
                {t('reservationSlots.copy.button')}
              </button>
            </article>
          )}

          <article className="panel form-panel">
            <div className="panel-header">
              <h3>{t('reservationSlots.request.title')}</h3>
              <p>{t('reservationSlots.request.description')}</p>
            </div>

            <div className="selected-slot-banner">
              {selectedSlot ? (
                <>
                  <strong>{formatDate(selectedSlot.startDateTime)} · {formatTime(selectedSlot.startDateTime)} - {formatTime(selectedSlot.endDateTime)}</strong>
                  <span>{selectedSlot.publicNote || t('reservationSlots.noPublicNote')}</span>
                </>
              ) : (
                <span>{t('reservationSlots.request.selectHint')}</span>
              )}
            </div>

            <div className="form-grid">
              <label>
                <span>{t('reservationSlots.request.firstName')}</span>
                <input value={bookingFirstName} onChange={(event) => setBookingFirstName(event.target.value)} />
              </label>
              <label>
                <span>{t('reservationSlots.request.lastName')}</span>
                <input value={bookingLastName} onChange={(event) => setBookingLastName(event.target.value)} />
              </label>
              <label>
                <span>{t('reservationSlots.request.phone')}</span>
                <input value={bookingPhoneNumber} onChange={(event) => setBookingPhoneNumber(event.target.value)} />
              </label>
              <label>
                <span>{t('reservationSlots.request.email')}</span>
                <input type="email" value={bookingEmail} onChange={(event) => setBookingEmail(event.target.value)} />
              </label>
              <label className="full-width">
                <span>{t('reservationSlots.request.examinationType')}</span>
                <select value={bookingExaminationTypeId} onChange={(event) => setBookingExaminationTypeId(event.target.value)}>
                  <option value="">{t('reservationSlots.request.selectExaminationType')}</option>
                  {examinationTypes.map(type => (
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
              disabled={!selectedSlotId || !availableSlots.some(slot => slot.id === selectedSlotId)}
            >
              {t('reservationSlots.request.blockButton')}
            </button>
          </article>
        </section>

        <section className="panel table-panel">
          <div className="panel-header">
            <h3>{t('reservationSlots.overview.title')}</h3>
            <p>
              {loadingSlots
                ? t('reservationSlots.overview.loading')
                : t('reservationSlots.overview.loaded', { count: sortedSlots.length, day: formatDate(`${selectedDate}T00:00:00`) })}
            </p>
          </div>

          <div className="slots-table-wrapper">
            <table className="slots-table">
              <thead>
                <tr>
                  <th>{t('reservationSlots.table.time')}</th>
                  <th>{t('reservationSlots.table.status')}</th>
                  <th>{t('reservationSlots.table.patient')}</th>
                  <th>{t('reservationSlots.table.examination')}</th>
                  <th>{t('reservationSlots.table.publicNote')}</th>
                  {canReviewSlots && <th>{t('reservationSlots.table.internalNote')}</th>}
                  <th>{t('reservationSlots.table.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {sortedSlots.length === 0 ? (
                  <tr>
                    <td colSpan={canReviewSlots ? 7 : 6} className="empty-row">
                      {t('reservationSlots.table.noSlots')}
                    </td>
                  </tr>
                ) : (
                  sortedSlots.flatMap(slot => {
                    const slotRow = (
                      <tr key={slot.id} className={selectedSlotId === slot.id ? 'selected-row' : ''}>
                        <td>
                          <div className="slot-time-cell">
                            <strong>{formatTime(slot.startDateTime)} - {formatTime(slot.endDateTime)}</strong>
                            <span>{formatDate(slot.startDateTime)}</span>
                          </div>
                        </td>
                        <td>
                          <span className={`status-badge status-${slot.status.toLowerCase()}`}>
                            {t(`reservationSlots.status.${slot.status.toLowerCase()}`)}
                          </span>
                        </td>
                        <td>{slot.personName || '-'}</td>
                        <td>{slot.examinationTypeName || '-'}</td>
                        <td>{slot.publicNote || '-'}</td>
                        {canReviewSlots && <td>{slot.internalNote || '-'}</td>}
                        <td>
                          <div className="action-stack">
                            {slot.status === 'AVAILABLE' && (
                              <button className="ghost-button" onClick={() => handleSelectSlot(slot)}>
                                {t('reservationSlots.table.select')}
                              </button>
                            )}

                            <button className="ghost-button" onClick={() => handleEditSlot(slot)}>
                              {t('reservationSlots.table.edit')}
                            </button>

                            {canReviewSlots && slot.status === 'BLOCKED' && (
                              <>
                                <button className="success-button" onClick={() => handleConfirmSlot(slot.id)}>
                                  {t('reservationSlots.table.confirm')}
                                </button>
                                <button className="danger-button" onClick={() => handleRejectSlot(slot.id)}>
                                  {t('reservationSlots.table.reject')}
                                </button>
                              </>
                            )}
                          </div>
                        </td>
                      </tr>
                    );

                    if (selectedSlotId !== slot.id) {
                      return [slotRow];
                    }

                    return [
                      slotRow,
                      <tr key={`${slot.id}-editor`} className="slot-editor-row">
                        <td colSpan={canReviewSlots ? 7 : 6} className="slot-editor-cell">
                          <div className="slot-editor">
                            <div className="panel-header">
                              <h3>{t('reservationSlots.editor.title')}</h3>
                              <p>{t('reservationSlots.editor.description')}</p>
                            </div>

                            <div className="selected-slot-banner">
                              <strong>
                                {formatDate(slot.startDateTime)} · {formatTime(slot.startDateTime)} - {formatTime(slot.endDateTime)}
                              </strong>
                              <span>{t(`reservationSlots.status.${slot.status.toLowerCase()}`)}</span>
                            </div>

                            <div className="form-grid">
                              <label className="full-width">
                                <span>{t('reservationSlots.editor.publicNote')}</span>
                                <textarea value={editPublicNote} onChange={(event) => setEditPublicNote(event.target.value)} rows={3} />
                              </label>
                              <label className="full-width">
                                <span>{t('reservationSlots.editor.internalNote')}</span>
                                <textarea value={editInternalNote} onChange={(event) => setEditInternalNote(event.target.value)} rows={3} />
                              </label>
                              <label>
                                <span>{t('reservationSlots.editor.status')}</span>
                                <select value={editStatus} onChange={(event) => setEditStatus(event.target.value as ReservationSlotStatus)}>
                                  <option value="AVAILABLE">{t('reservationSlots.status.available')}</option>
                                  <option value="UNAVAILABLE">{t('reservationSlots.status.unavailable')}</option>
                                  <option value="BLOCKED">{t('reservationSlots.status.blocked')}</option>
                                  <option value="RESERVED">{t('reservationSlots.status.reserved')}</option>
                                </select>
                              </label>
                            </div>

                            <div className="action-stack slot-editor-actions">
                              <button className="success-button" onClick={handleUpdateSelectedSlot}>
                                {t('reservationSlots.editor.saveButton')}
                              </button>
                              {(slot.status === 'BLOCKED' || slot.status === 'RESERVED') && (
                                <button className="ghost-button" onClick={handleReleaseSelectedSlot}>
                                  {t('reservationSlots.editor.releaseButton')}
                                </button>
                              )}
                              <button className="danger-button" onClick={handleDeleteSelectedSlot}>
                                {t('reservationSlots.editor.deleteButton')}
                              </button>
                            </div>
                          </div>
                        </td>
                      </tr>
                    ];
                  })
                )}
              </tbody>
            </table>
          </div>
        </section>
      </main>
    </div>
  );
};

export default ReservationSlotsPage;
