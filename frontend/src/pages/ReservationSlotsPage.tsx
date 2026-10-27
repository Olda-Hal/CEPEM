import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AppHeader } from '../components/AppHeader';
import { useAuth } from '../contexts/AuthContext';
import { apiClient, ApiError } from '../utils/api';
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


const formatDateInput = (date: Date) => date.toISOString().slice(0, 10);

const formatDate = (value: string) =>
  new Intl.DateTimeFormat('cs-CZ', { dateStyle: 'medium' }).format(new Date(value));

const formatTime = (value: string) =>
  new Intl.DateTimeFormat('cs-CZ', { hour: '2-digit', minute: '2-digit' }).format(new Date(value));

const createLocalDateTime = (dateValue: string, timeValue: string) => new Date(`${dateValue}T${timeValue}:00`);
import { useToast } from '../contexts/ToastContext';
import SlotCreationPanel from '../components/reservations/SlotCreationPanel';
import BookingPanel from '../components/reservations/BookingPanel';

export const ReservationSlotsPage: React.FC = () => {
  const { user } = useAuth();
  const { t, i18n } = useTranslation();
  const toast = useToast();

  const [hospitals, setHospitals] = useState<Hospital[]>([]);
  const [selectedHospitalId, setSelectedHospitalId] = useState('');
  const [selectedDate, setSelectedDate] = useState(formatDateInput(new Date()));
  const [slots, setSlots] = useState<ReservationSlot[]>([]);
  const [loadingSlots, setLoadingSlots] = useState(false);
  const [examinationTypes, setExaminationTypes] = useState<ExaminationType[]>([]);
  const [selectedSlotId, setSelectedSlotId] = useState<number | null>(null);




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

  const handleSelectSlot = (slot: ReservationSlot) => {
    setSelectedSlotId(slot.id);
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
      toast.success(t('reservationSlots.messages.updatedSuccess'));
    } catch (error) {
      const errorMessage = error instanceof ApiError ? error.message : t('reservationSlots.errors.updateFailed');
      toast.error(errorMessage);
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
      toast.success(t('reservationSlots.messages.releasedSuccess'));
    } catch (error) {
      const errorMessage = error instanceof ApiError ? error.message : t('reservationSlots.errors.releaseFailed');
      toast.error(errorMessage);
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
      await refreshSlots();
      toast.success(t('reservationSlots.messages.deletedSuccess'));
    } catch (error) {
      const errorMessage = error instanceof ApiError ? error.message : t('reservationSlots.errors.deleteFailed');
      toast.error(errorMessage);
      console.error(t('reservationSlots.errors.deleteFailed'), error);
    }
  };

  const handleConfirmSlot = async (slotId: number) => {
    if (!user?.id) {
      toast.error(t('errors.userNotFound'));
      return;
    }

    const request: ConfirmReservationSlotRequest = {
      doctorId: user.id
    };

    try {
      await apiClient.post(`/api/reservations/slots/${slotId}/confirm`, request);
      await refreshSlots();
      toast.success(t('reservationSlots.messages.confirmedSuccess'));
    } catch (error) {
      const errorMessage = error instanceof ApiError ? error.message : t('reservationSlots.errors.confirmFailed');
      toast.error(errorMessage);
      console.error(t('reservationSlots.errors.confirmFailed'), error);
    }
  };

  const handleRejectSlot = async (slotId: number) => {
    try {
      await apiClient.post(`/api/reservations/slots/${slotId}/reject`);
      await refreshSlots();
      toast.success(t('reservationSlots.messages.rejectedSuccess'));
    } catch (error) {
      const errorMessage = error instanceof ApiError ? error.message : t('reservationSlots.errors.rejectFailed');
      toast.error(errorMessage);
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
            <SlotCreationPanel
              hospitalId={selectedHospitalId}
              targetDate={selectedDate}
              onDateChange={setSelectedDate}
              onSlotsChanged={async () => {
                setSelectedSlotId(null);
                await refreshSlots();
              }}
            />
          )}

          <BookingPanel
            selectedSlot={selectedSlot}
            selectedSlotId={selectedSlotId}
            isSlotAvailable={!!selectedSlotId && availableSlots.some(slot => slot.id === selectedSlotId)}
            examinationTypes={examinationTypes}
            onBookingCleared={() => setSelectedSlotId(null)}
            onSlotsChanged={refreshSlots}
          />
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
