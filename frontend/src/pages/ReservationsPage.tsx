import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AppHeader } from '../components/AppHeader';
import { useAuth } from '../contexts/AuthContext';
import { apiClient } from '../utils/api';
import { isAdmin } from '../utils/roles';
import {
  CreateReservationRequest,
  DoctorExaminationRoom,
  EmployeeListItem,
  ExaminationRoom,
  EventOptions,
  Hospital,
  Patient,
  PatientSearchResponse,
  Reservation
} from '../types';
import './ReservationsPage.css';

const DEFAULT_DURATION_MINUTES = 30;

export const ReservationsPage: React.FC = () => {
  const { user } = useAuth();
  const { t, i18n } = useTranslation();
  const [hospitals, setHospitals] = useState<Hospital[]>([]);
  const [hospitalAddress, setHospitalAddress] = useState('');
  const [hospitalId, setHospitalId] = useState('1');
  const [rooms, setRooms] = useState<ExaminationRoom[]>([]);
  const [roomsLoading, setRoomsLoading] = useState(false);
  const [employees, setEmployees] = useState<EmployeeListItem[]>([]);
  const [selectedDoctorId, setSelectedDoctorId] = useState('');
  const [selectedRoomId, setSelectedRoomId] = useState('');
  const [roomName, setRoomName] = useState('');
  const [roomDescription, setRoomDescription] = useState('');
  const [assignedRooms, setAssignedRooms] = useState<DoctorExaminationRoom[]>([]);
  const [selectedCalendarRoomId, setSelectedCalendarRoomId] = useState('');
  const [reservations, setReservations] = useState<Reservation[]>([]);
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [patientQuery, setPatientQuery] = useState('');
  const [patientResults, setPatientResults] = useState<Patient[]>([]);
  const [selectedPatientId, setSelectedPatientId] = useState('');
  const [examinationTypeId, setExaminationTypeId] = useState('');
  const [startDate, setStartDate] = useState('');
  const [startTime, setStartTime] = useState('');
  const [durationMinutes, setDurationMinutes] = useState(DEFAULT_DURATION_MINUTES);
  const [notes, setNotes] = useState('');
  const [eventOptions, setEventOptions] = useState<EventOptions | null>(null);
  const [loadingReservations, setLoadingReservations] = useState(false);
  const [currentMonth, setCurrentMonth] = useState(new Date());
  const [selectedDate, setSelectedDate] = useState<Date | null>(null);
  const [examinationTypes, setExaminationTypes] = useState<Array<{ id: number; name: string }>>([]);
  const [examinationTypeName, setExaminationTypeName] = useState('');

  const isUserAdmin = isAdmin(user);

  useEffect(() => {
    if (isUserAdmin) {
      loadEmployees();
      loadHospitals();
      loadExaminationTypes();
    }
  }, [isUserAdmin]);

  useEffect(() => {
    if (user?.id) {
      loadAssignedRooms(user.id);
    }
  }, [user?.id]);

  useEffect(() => {
    loadEventOptions();
  }, [i18n.language]);

  useEffect(() => {
    if (!selectedCalendarRoomId) return;
    loadRoomReservations(selectedCalendarRoomId);
  }, [selectedCalendarRoomId, fromDate, toDate]);

  useEffect(() => {
    const timeoutId = setTimeout(() => {
      if (patientQuery.trim().length < 2) {
        setPatientResults([]);
        return;
      }
      searchPatients(patientQuery.trim());
    }, 300);

    return () => clearTimeout(timeoutId);
  }, [patientQuery]);

  useEffect(() => {
    if (assignedRooms.length > 0 && !selectedCalendarRoomId) {
      setSelectedCalendarRoomId(String(assignedRooms[0].examinationRoomId));
    }
  }, [assignedRooms, selectedCalendarRoomId]);

  const loadHospitals = async () => {
    try {
      const response = await apiClient.get<Hospital[]>('/api/hospitals');
      setHospitals(response);
      if (response.length > 0 && !hospitalId) {
        setHospitalId(String(response[0].id));
      }
    } catch (error) {
      console.error(t('errors.loadingHospitals'), error);
    }
  };

  const loadEmployees = async () => {
    try {
      const response = await apiClient.get<EmployeeListItem[]>('/api/admin/employees');
      setEmployees(response);
    } catch (error) {
      console.error(t('errors.loadingEmployees'), error);
    }
  };

  const loadRooms = async () => {
    if (!hospitalId) return;
    try {
      setRoomsLoading(true);
      const response = await apiClient.get<ExaminationRoom[]>(`/api/examinationrooms/hospital/${hospitalId}`);
      setRooms(response);
    } catch (error) {
      console.error(t('errors.loadingRooms'), error);
    } finally {
      setRoomsLoading(false);
    }
  };

  const loadAssignedRooms = async (doctorId: number) => {
    try {
      const response = await apiClient.get<DoctorExaminationRoom[]>(`/api/doctorexaminationrooms/doctor/${doctorId}`);
      setAssignedRooms(response);
    } catch (error) {
      console.error(t('errors.loadingRooms'), error);
    }
  };

  const loadEventOptions = async () => {
    try {
      const response = await apiClient.get<EventOptions>(`/api/events/options?language=${i18n.language}`);
      setEventOptions(response);
    } catch (error) {
      console.error(t('errors.loadingExaminationTypes'), error);
    }
  };

  const loadExaminationTypes = async () => {
    try {
      const response = await apiClient.get<Array<{ id: number; name: string }>>(`/api/examinationtypes?language=${i18n.language}`);
      setExaminationTypes(response);
    } catch (error) {
      console.error(t('errors.loadingExaminationTypes'), error);
    }
  };

  const handleCreateExaminationType = async () => {
    if (!examinationTypeName.trim()) return;
    try {
      await apiClient.post('/api/examinationtypes', { name: examinationTypeName });
      setExaminationTypeName('');
      await loadExaminationTypes();
      alert(t('reservations.examinationTypeCreated'));
    } catch (error) {
      console.error(t('errors.creatingExaminationType'), error);
      alert(t('errors.creatingExaminationType'));
    }
  };

  const handleDeleteExaminationType = async (id: number) => {
    if (!window.confirm(t('common.confirmDelete'))) return;
    try {
      await apiClient.delete(`/api/examinationtypes/${id}`);
      await loadExaminationTypes();
      alert(t('reservations.examinationTypeDeleted'));
    } catch (error: any) {
      const errorMessage = error.response?.data || error.message || t('errors.deletingExaminationType');
      alert(errorMessage);
      console.error(t('errors.deletingExaminationType'), error);
    }
  };

  const loadRoomReservations = async (roomId: string) => {
    try {
      setLoadingReservations(true);
      const params = new URLSearchParams();
      if (fromDate) params.append('from', new Date(`${fromDate}T00:00:00`).toISOString());
      if (toDate) params.append('to', new Date(`${toDate}T23:59:59`).toISOString());
      const query = params.toString() ? `?${params.toString()}` : '';
      const response = await apiClient.get<Reservation[]>(`/api/reservations/room/${roomId}${query}`);
      setReservations(response);
    } catch (error) {
      console.error(t('errors.loadingReservations'), error);
    } finally {
      setLoadingReservations(false);
    }
  };

  const searchPatients = async (query: string) => {
    try {
      const params = new URLSearchParams({
        page: '0',
        limit: '10',
        search: query,
        sortBy: 'lastName',
        sortOrder: 'asc'
      });
      const response = await apiClient.get<PatientSearchResponse>(`/api/patients/search?${params.toString()}`);
      setPatientResults(response.patients);
    } catch (error) {
      console.error(t('errors.loadingPatients'), error);
    }
  };

  const handleCreateHospital = async () => {
    if (!hospitalAddress.trim()) return;
    try {
      await apiClient.post('/api/hospitals', {
        address: hospitalAddress
      });
      setHospitalAddress('');
      loadHospitals();
    } catch (error) {
      console.error(t('errors.creatingHospital'), error);
    }
  };

  const handleCreateRoom = async () => {
    if (!roomName || !hospitalId) return;
    try {
      await apiClient.post('/api/examinationrooms', {
        name: roomName,
        description: roomDescription,
        hospitalId: Number(hospitalId)
      });
      setRoomName('');
      setRoomDescription('');
      loadRooms();
    } catch (error) {
      console.error(t('errors.creatingRoom'), error);
    }
  };

  const handleAssignRoom = async () => {
    if (!selectedDoctorId || !selectedRoomId) return;
    try {
      await apiClient.post('/api/doctorexaminationrooms', {
        DoctorId: Number(selectedDoctorId),
        ExaminationRoomId: Number(selectedRoomId)
      });
      alert(t('reservations.roomAssignedSuccess'));
      setSelectedDoctorId('');
      setSelectedRoomId('');
      if (user?.id && Number(selectedDoctorId) === user.id) {
        loadAssignedRooms(user.id);
      }
    } catch (error: any) {
      const errorMessage = error.response?.data || error.message || t('errors.assigningRoom');
      alert(errorMessage);
      console.error(t('errors.assigningRoom'), error);
    }
  };

  const handleCreateReservation = async () => {
    if (!selectedPatientId || !selectedCalendarRoomId || !examinationTypeId || !startDate || !startTime) return;
    if (!user?.id) {
      alert(t('errors.userNotFound'));
      return;
    }

    const start = new Date(`${startDate}T${startTime}`);
    const end = new Date(start.getTime() + Number(durationMinutes) * 60000);

    const request: CreateReservationRequest = {
      doctorId: user.id,
      patientId: Number(selectedPatientId),
      examinationRoomId: Number(selectedCalendarRoomId),
      examinationTypeId: Number(examinationTypeId),
      startDateTime: start.toISOString(),
      endDateTime: end.toISOString(),
      notes: notes.trim() || undefined
    };

    try {
      await apiClient.post('/api/reservations', request);
      alert(t('reservations.reservationCreated'));
      setNotes('');
      setSelectedPatientId('');
      setPatientQuery('');
      setStartDate('');
      setStartTime('');
      loadRoomReservations(selectedCalendarRoomId);
    } catch (error: any) {
      const errorMessage = error.response?.data || error.message || t('errors.creatingReservation');
      alert(errorMessage);
      console.error(t('errors.creatingReservation'), error);
    }
  };

  const formatDateTime = (dateString: string) => {
    const locale = i18n.language === 'cs' ? 'cs-CZ' : 'en-US';
    return new Date(dateString).toLocaleString(locale);
  };

  const getDaysInMonth = (date: Date) => {
    const year = date.getFullYear();
    const month = date.getMonth();
    const firstDay = new Date(year, month, 1);
    const lastDay = new Date(year, month + 1, 0);
    const daysInMonth = lastDay.getDate();
    const startingDayOfWeek = firstDay.getDay();
    const days: Array<{ date: Date; isCurrentMonth: boolean; hasReservations: boolean }> = [];

    const prevMonthLastDay = new Date(year, month, 0).getDate();
    for (let i = startingDayOfWeek - 1; i >= 0; i--) {
      days.push({
        date: new Date(year, month - 1, prevMonthLastDay - i),
        isCurrentMonth: false,
        hasReservations: false
      });
    }

    for (let day = 1; day <= daysInMonth; day++) {
      const currentDate = new Date(year, month, day);
      const hasReservations = reservations.some(r => {
        const reservationDate = new Date(r.startDateTime);
        return reservationDate.toDateString() === currentDate.toDateString();
      });
      days.push({
        date: currentDate,
        isCurrentMonth: true,
        hasReservations
      });
    }

    const remainingDays = 42 - days.length;
    for (let day = 1; day <= remainingDays; day++) {
      days.push({
        date: new Date(year, month + 1, day),
        isCurrentMonth: false,
        hasReservations: false
      });
    }

    return days;
  };

  const changeMonth = (direction: number) => {
    setCurrentMonth(new Date(currentMonth.getFullYear(), currentMonth.getMonth() + direction, 1));
  };

  const handleDayClick = (date: Date) => {
    setSelectedDate(date);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const dateStr = `${year}-${month}-${day}`;
    setFromDate(dateStr);
    setToDate(dateStr);
  };

  const getMonthName = (date: Date) => {
    const locale = i18n.language === 'cs' ? 'cs-CZ' : 'en-US';
    return date.toLocaleDateString(locale, { month: 'long', year: 'numeric' });
  };

  const getDayNames = () => {
    const locale = i18n.language === 'cs' ? 'cs-CZ' : 'en-US';
    const days = [];
    const baseDate = new Date(2024, 0, 1);
    for (let i = 0; i < 7; i++) {
      const day = new Date(baseDate);
      day.setDate(baseDate.getDate() + i);
      days.push(day.toLocaleDateString(locale, { weekday: 'short' }));
    }
    return days;
  };

  const isToday = (date: Date) => {
    const today = new Date();
    return date.toDateString() === today.toDateString();
  };

  const availableRooms = useMemo(() => {
    if (isUserAdmin) return rooms;
    return assignedRooms.map(ar => ({
      id: ar.examinationRoomId,
      name: ar.roomName,
      description: '',
      hospitalId: ar.hospitalId,
      isActive: true,
      createdAt: '',
      updatedAt: ''
    }));
  }, [assignedRooms, isUserAdmin, rooms]);

  return (
    <div className="reservations-container">
      <AppHeader sectionTitle={t('reservations.title')} />

      <div className="reservations-content">
        {isUserAdmin && (
          <div className="admin-section">
            <h2>{t('reservations.adminSection')}</h2>

            <div className="admin-grid">
              <div className="admin-card">
                <h3>{t('reservations.hospitalsTitle')}</h3>
                <div className="form-row">
                  <label>{t('reservations.hospitalAddress')}</label>
                  <input
                    value={hospitalAddress}
                    onChange={(e) => setHospitalAddress(e.target.value)}
                    placeholder={t('reservations.hospitalAddressPlaceholder')}
                  />
                </div>
                <button className="primary" onClick={handleCreateHospital}>
                  {t('reservations.createHospital')}
                </button>
                <div className="list">
                  {hospitals.map(hospital => (
                    <div key={hospital.id} className="list-item">
                      <span>{hospital.name || t('reservations.noAddress')}</span>
                      <span>ID: {hospital.id}</span>
                    </div>
                  ))}
                </div>
              </div>

              <div className="admin-card">
                <h3>{t('reservations.roomsTitle')}</h3>
                <div className="form-row">
                  <label>{t('reservations.selectHospital')}</label>
                  <select
                    value={hospitalId}
                    onChange={(e) => setHospitalId(e.target.value)}
                  >
                    <option value="">{t('reservations.selectHospitalPlaceholder')}</option>
                    {hospitals.map(hospital => (
                      <option key={hospital.id} value={hospital.id}>
                        {hospital.name || `Hospital ${hospital.id}`}
                      </option>
                    ))}
                  </select>
                  <button onClick={loadRooms} disabled={roomsLoading}>
                    {roomsLoading ? t('common.loading') : t('reservations.loadRooms')}
                  </button>
                </div>
                <div className="form-row">
                  <label>{t('reservations.roomName')}</label>
                  <input
                    value={roomName}
                    onChange={(e) => setRoomName(e.target.value)}
                  />
                </div>
                <div className="form-row">
                  <label>{t('reservations.roomDescription')}</label>
                  <textarea
                    value={roomDescription}
                    onChange={(e) => setRoomDescription(e.target.value)}
                  />
                </div>
                <button className="primary" onClick={handleCreateRoom}>
                  {t('reservations.createRoom')}
                </button>
                <div className="list">
                  {rooms.map(room => (
                    <div key={room.id} className="list-item">
                      <span>{room.name}</span>
                      <span>{t('reservations.hospitalIdLabel', { id: room.hospitalId })}</span>
                    </div>
                  ))}
                </div>
              </div>

              <div className="admin-card">
                <h3>{t('reservations.assignRoomsTitle')}</h3>
                <div className="form-row">
                  <label>{t('reservations.selectDoctor')}</label>
                  <select
                    value={selectedDoctorId}
                    onChange={(e) => setSelectedDoctorId(e.target.value)}
                  >
                    <option value="">{t('reservations.selectDoctorPlaceholder')}</option>
                    {employees.map(emp => (
                      <option key={emp.employeeId} value={emp.employeeId}>
                        {emp.fullName}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="form-row">
                  <label>{t('reservations.selectRoom')}</label>
                  <select
                    value={selectedRoomId}
                    onChange={(e) => setSelectedRoomId(e.target.value)}
                  >
                    <option value="">{t('reservations.selectRoomPlaceholder')}</option>
                    {rooms.map(room => (
                      <option key={room.id} value={room.id}>
                        {room.name}
                      </option>
                    ))}
                  </select>
                </div>
                <button className="primary" onClick={handleAssignRoom}>
                  {t('reservations.assignRoom')}
                </button>
              </div>

              <div className="admin-card">
                <h3>{t('reservations.examinationTypesTitle')}</h3>
                <div className="form-row">
                  <label>{t('reservations.examinationTypeName')}</label>
                  <input
                    value={examinationTypeName}
                    onChange={(e) => setExaminationTypeName(e.target.value)}
                    placeholder={t('reservations.examinationTypeNamePlaceholder')}
                  />
                </div>
                <button className="primary" onClick={handleCreateExaminationType}>
                  {t('reservations.createExaminationType')}
                </button>
                <div className="list">
                  {examinationTypes.map(type => (
                    <div key={type.id} className="list-item">
                      <span>{type.name}</span>
                      <button
                        onClick={() => handleDeleteExaminationType(type.id)}
                        style={{
                          background: 'var(--color-error)',
                          color: 'white',
                          border: 'none',
                          padding: '4px 8px',
                          borderRadius: '4px',
                          cursor: 'pointer',
                          fontSize: '0.8rem'
                        }}
                      >
                        {t('common.delete')}
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        )}

        <div className="calendar-section">
          <h2>{t('reservations.calendarTitle')}</h2>
          
          <div className="calendar-layout">
            <div className="monthly-calendar">
              <div className="month-header">
                <h3 className="month-title">{getMonthName(currentMonth)}</h3>
                <div className="month-nav">
                  <button className="nav-button" onClick={() => changeMonth(-1)}>‹</button>
                  <button className="nav-button" onClick={() => changeMonth(1)}>›</button>
                </div>
              </div>
              <div className="calendar-grid">
                {getDayNames().map((day, index) => (
                  <div key={index} className="calendar-day-header">{day}</div>
                ))}
                {getDaysInMonth(currentMonth).map((day, index) => (
                  <div
                    key={index}
                    className={`calendar-day ${!day.isCurrentMonth ? 'other-month' : ''} ${isToday(day.date) ? 'today' : ''} ${day.hasReservations ? 'has-reservations' : ''}`}
                    onClick={() => day.isCurrentMonth && handleDayClick(day.date)}
                  >
                    {day.date.getDate()}
                  </div>
                ))}
              </div>
            </div>

            <div className="calendar-filters">
              <div className="form-row">
                <label>{t('reservations.selectRoom')}</label>
                <select
                  value={selectedCalendarRoomId}
                  onChange={(e) => setSelectedCalendarRoomId(e.target.value)}
                >
                  <option value="">{t('reservations.selectRoomPlaceholder')}</option>
                  {availableRooms.map(room => (
                    <option key={room.id} value={room.id}>
                      {room.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-row">
                <label>{t('reservations.fromDate')}</label>
                <input
                  type="date"
                  value={fromDate}
                  onChange={(e) => setFromDate(e.target.value)}
                />
              </div>
              <div className="form-row">
                <label>{t('reservations.toDate')}</label>
                <input
                  type="date"
                  value={toDate}
                  onChange={(e) => setToDate(e.target.value)}
                />
              </div>
            </div>
          </div>

          <div className="calendar-content">
            {loadingReservations ? (
              <div className="empty-state">
                <div className="empty-state-icon">⏳</div>
                <div className="empty-state-text">{t('common.loading')}</div>
              </div>
            ) : reservations.length === 0 ? (
              <div className="empty-state">
                <div className="empty-state-icon">📅</div>
                <div className="empty-state-text">{t('reservations.noReservations')}</div>
              </div>
            ) : (
              <div className="reservations-table-container">
                <table>
                  <thead>
                    <tr>
                      <th>{t('reservations.patient')}</th>
                      <th>{t('reservations.doctor')}</th>
                      <th>{t('reservations.examinationType')}</th>
                      <th>{t('reservations.start')}</th>
                      <th>{t('reservations.end')}</th>
                      <th>{t('reservations.notes')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {reservations.map(reservation => (
                      <tr key={reservation.id}>
                        <td>{reservation.patientName || reservation.patientId}</td>
                        <td>{reservation.doctorName || reservation.doctorId}</td>
                        <td>{reservation.examinationTypeName || reservation.examinationTypeId}</td>
                        <td>{formatDateTime(reservation.startDateTime)}</td>
                        <td>{formatDateTime(reservation.endDateTime)}</td>
                        <td>{reservation.notes || '-'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          <div className="reservation-form">
            <h3>{t('reservations.createReservationTitle')}</h3>
            <div className="form-grid">
              <div className="form-row">
                <label>{t('reservations.patientSearch')}</label>
                <div className="patient-search-container">
                  <input
                    type="text"
                    value={patientQuery}
                    onChange={(e) => setPatientQuery(e.target.value)}
                    placeholder={t('reservations.patientSearchPlaceholder')}
                    className="patient-search-input"
                  />
                  {patientResults.length > 0 && patientQuery && (
                    <div className="patient-search-results">
                      {patientResults.map(patient => (
                        <div
                          key={patient.id}
                          className="patient-search-result-item"
                          onClick={() => {
                            setSelectedPatientId(String(patient.id));
                            setPatientQuery(patient.fullName);
                            setPatientResults([]);
                          }}
                        >
                          <div className="patient-name">{patient.fullName}</div>
                          {patient.uid && <div className="patient-uid">{patient.uid}</div>}
                        </div>
                      ))}
                    </div>
                  )}
                </div>
                {selectedPatientId && (
                  <div className="selected-patient-info">
                    {patientResults.find(p => String(p.id) === selectedPatientId)?.fullName || 'Selected'}
                  </div>
                )}
              </div>
              <div className="form-row">
                <label>{t('reservations.examinationType')}</label>
                <select
                  value={examinationTypeId}
                  onChange={(e) => setExaminationTypeId(e.target.value)}
                >
                  <option value="">{t('reservations.selectExaminationType')}</option>
                  {eventOptions?.examinationTypes.map(type => (
                    <option key={type.id} value={type.id}>
                      {type.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-row">
                <label>{t('reservations.startDate')}</label>
                <input
                  type="date"
                  value={startDate}
                  onChange={(e) => setStartDate(e.target.value)}
                />
              </div>
              <div className="form-row">
                <label>{t('reservations.startTime')}</label>
                <input
                  type="time"
                  value={startTime}
                  onChange={(e) => setStartTime(e.target.value)}
                />
              </div>
              <div className="form-row">
                <label>{t('reservations.duration')}</label>
                <input
                  type="number"
                  value={durationMinutes}
                  min={5}
                  step={5}
                  onChange={(e) => setDurationMinutes(Number(e.target.value))}
                />
              </div>
              <div className="form-row">
                <label>{t('reservations.notes')}</label>
                <textarea
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                />
              </div>
            </div>
            <button className="primary" onClick={handleCreateReservation}>
              {t('reservations.createReservation')}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
