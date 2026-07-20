import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AppHeader } from '../components/AppHeader';
import { useAuth } from '../contexts/AuthContext';
import { apiClient } from '../utils/api';
import { hasRole } from '../utils/roles';
import {
  DoctorExaminationRoom,
  EmployeeListItem,
  ExaminationRoom,
  ExaminationType,
  Hospital
} from '../types';
import './CenterManagementPage.css';

interface HospitalPayload {
  name?: string;
  street?: string;
  city?: string;
  postalCode?: string;
  country?: string;
  countryScopeId?: number;
  active?: boolean;
}

interface AssignmentViewModel {
  id: number;
  doctorId: number;
  doctorName: string;
  examinationRoomId: number;
  roomName: string;
}

const countryScopeByCode: Record<string, number> = {
  CZ: 203,
  DE: 276,
  SK: 703,
  AT: 40,
  PL: 616,
  NL: 528
};

const countryOptions = Object.keys(countryScopeByCode);

const normalizeCountryCode = (countryCode?: string) => {
  if (!countryCode) {
    return 'CZ';
  }

  return countryCode.trim().toUpperCase().slice(0, 2);
};

const emptyCenterForm = {
  name: '',
  street: '',
  city: '',
  postalCode: '',
  country: ''
};

const CenterManagementPage: React.FC = () => {
  const { t, i18n } = useTranslation();
  const { user } = useAuth();

  const canManageCenters = hasRole(user, 'Country Admin') || hasRole(user, 'SysAdmin') || hasRole(user, 'Admin');
  const isRestrictedCountryAdmin = hasRole(user, 'Country Admin') && !hasRole(user, 'SysAdmin') && !hasRole(user, 'Admin');
  const actorCountryCode = normalizeCountryCode(user?.countryCode);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const [hospitals, setHospitals] = useState<Hospital[]>([]);
  const [selectedHospitalId, setSelectedHospitalId] = useState<number | null>(null);

  const [createCenterForm, setCreateCenterForm] = useState(emptyCenterForm);
  const [editCenterForm, setEditCenterForm] = useState(emptyCenterForm);
  const [createCenterCountryCode, setCreateCenterCountryCode] = useState(actorCountryCode);
  const [editCenterCountryCode, setEditCenterCountryCode] = useState(actorCountryCode);

  const [rooms, setRooms] = useState<ExaminationRoom[]>([]);
  const [personnel, setPersonnel] = useState<EmployeeListItem[]>([]);
  const [assignments, setAssignments] = useState<AssignmentViewModel[]>([]);

  const [newRoomName, setNewRoomName] = useState('');
  const [newRoomDescription, setNewRoomDescription] = useState('');
  const [selectedDoctorId, setSelectedDoctorId] = useState<number | null>(null);
  const [selectedRoomId, setSelectedRoomId] = useState<number | null>(null);

  const [allExaminationTypes, setAllExaminationTypes] = useState<ExaminationType[]>([]);
  const [allowedExaminationTypeIds, setAllowedExaminationTypeIds] = useState<number[]>([]);
  const [savingAllowedTypes, setSavingAllowedTypes] = useState(false);

  const selectedHospital = useMemo(
    () => hospitals.find((hospital) => hospital.id === selectedHospitalId) ?? null,
    [hospitals, selectedHospitalId]
  );

  const personnelNameMap = useMemo(() => {
    const map = new Map<number, string>();
    personnel.forEach((person) => {
      map.set(person.employeeId, person.fullName);
    });
    return map;
  }, [personnel]);

  const getHospitalTitle = (hospital: Hospital) => {
    const name = hospital.name?.trim();
    return name && name.length > 0 ? name : t('centerManagement.fallbackCenter', { id: hospital.id });
  };

  const getHospitalAddress = (hospital: Hospital) => {
    const address = hospital.address;
    if (!address) {
      return '';
    }

    return [address.street, address.city, address.postalCode, address.country]
      .filter((part) => part && part.trim().length > 0)
      .join(', ');
  };

  const loadHospitalsAndDoctors = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const hospitalsResponse = await apiClient.get<Hospital[]>('/api/hospitals');
      setHospitals(hospitalsResponse);

      if (!selectedHospitalId && hospitalsResponse.length > 0) {
        setSelectedHospitalId(hospitalsResponse[0].id);
      }

      try {
        const employees = await apiClient.get<EmployeeListItem[]>('/api/admin/employees');
        setPersonnel(employees.filter((employee) => employee.active));
      } catch (personnelError) {
        console.error('Error loading personnel for center management', personnelError);
        setPersonnel([]);
      }

      const examinationTypes = await apiClient.get<ExaminationType[]>(`/api/examinationtypes?language=${i18n.language}`);
      setAllExaminationTypes(examinationTypes);
    } catch (loadError) {
      console.error('Error loading center management data', loadError);
      setError(t('centerManagement.errors.loadCenters'));
    } finally {
      setLoading(false);
    }
  }, [i18n.language, selectedHospitalId, t]);

  const loadSelectedCenterData = useCallback(async () => {
    if (!selectedHospitalId) {
      setRooms([]);
      setAssignments([]);
      setAllowedExaminationTypeIds([]);
      return;
    }

    try {
      const [roomsResponse, allowedTypesResponse] = await Promise.all([
        apiClient.get<ExaminationRoom[]>(`/api/examinationrooms/hospital/${selectedHospitalId}`),
        apiClient.get<ExaminationType[]>(`/api/hospitals/${selectedHospitalId}/examination-types?language=${i18n.language}`)
      ]);

      setRooms(roomsResponse);
      setAllowedExaminationTypeIds(allowedTypesResponse.map((item) => item.id));
    } catch (loadError) {
      console.error('Error loading center detail data', loadError);
      setError(t('centerManagement.errors.loadCenterDetail'));
      setRooms([]);
      setAllowedExaminationTypeIds([]);
    }
  }, [i18n.language, selectedHospitalId, t]);

  const loadAssignments = useCallback(async () => {
    if (!selectedHospitalId || personnel.length === 0) {
      setAssignments([]);
      return;
    }

    const settledResponses = await Promise.allSettled(
      personnel.map((person) =>
        apiClient.get<DoctorExaminationRoom[]>(`/api/doctorexaminationrooms/doctor/${person.employeeId}`)
      )
    );

    const assignmentRows: AssignmentViewModel[] = [];

    settledResponses.forEach((result, index) => {
      if (result.status !== 'fulfilled') {
        console.error('Error loading doctor assignments', result.reason);
        return;
      }

      const person = personnel[index];
      result.value
        .filter((assignment) => assignment.hospitalId === selectedHospitalId)
        .forEach((assignment) => {
          assignmentRows.push({
            id: assignment.id,
            doctorId: assignment.doctorId,
            doctorName: person?.fullName || personnelNameMap.get(assignment.doctorId) || t('centerManagement.unknownPersonnel'),
            examinationRoomId: assignment.examinationRoomId,
            roomName: assignment.roomName
          });
        });
    });

    assignmentRows.sort((left, right) => {
      if (left.roomName === right.roomName) {
        return left.doctorName.localeCompare(right.doctorName);
      }

      return left.roomName.localeCompare(right.roomName);
    });

    setAssignments(assignmentRows);
  }, [personnelNameMap, personnel, selectedHospitalId, t]);

  useEffect(() => {
    if (!canManageCenters) {
      setLoading(false);
      return;
    }

    void loadHospitalsAndDoctors();
  }, [canManageCenters, loadHospitalsAndDoctors]);

  useEffect(() => {
    if (selectedHospital) {
      setEditCenterForm({
        name: selectedHospital.name ?? '',
        street: selectedHospital.address?.street ?? '',
        city: selectedHospital.address?.city ?? '',
        postalCode: selectedHospital.address?.postalCode ?? '',
        country: selectedHospital.address?.country ?? ''
      });
      setEditCenterCountryCode(normalizeCountryCode(selectedHospital.address?.country));
    } else {
      setEditCenterForm(emptyCenterForm);
      setEditCenterCountryCode(actorCountryCode);
    }
  }, [actorCountryCode, selectedHospital]);

  useEffect(() => {
    if (isRestrictedCountryAdmin) {
      setCreateCenterCountryCode(actorCountryCode);
      setEditCenterCountryCode(actorCountryCode);
    }
  }, [actorCountryCode, isRestrictedCountryAdmin]);

  useEffect(() => {
    if (!canManageCenters) {
      return;
    }

    void loadSelectedCenterData();
  }, [canManageCenters, loadSelectedCenterData]);

  useEffect(() => {
    if (!canManageCenters) {
      return;
    }

    void loadAssignments();
  }, [canManageCenters, loadAssignments]);

  const refreshAll = async () => {
    await loadHospitalsAndDoctors();
    await loadSelectedCenterData();
    await loadAssignments();
  };

  const handleCreateCenter = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);
    setSuccess(null);

    if (!createCenterForm.name.trim()) {
      setError(t('centerManagement.errors.centerNameRequired'));
      return;
    }

    const payload: HospitalPayload = {
      name: createCenterForm.name.trim(),
      street: createCenterForm.street.trim() || undefined,
      city: createCenterForm.city.trim() || undefined,
      postalCode: createCenterForm.postalCode.trim() || undefined,
      country: createCenterCountryCode,
      countryScopeId: countryScopeByCode[createCenterCountryCode] ?? 203
    };

    try {
      await apiClient.post('/api/hospitals', payload);
      setCreateCenterForm(emptyCenterForm);
      setCreateCenterCountryCode(actorCountryCode);
      setSuccess(t('centerManagement.messages.centerCreated'));
      await loadHospitalsAndDoctors();
    } catch (createError) {
      console.error('Error creating center', createError);
      setError(t('centerManagement.errors.createCenter'));
    }
  };

  const handleUpdateCenter = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!selectedHospitalId) {
      return;
    }

    setError(null);
    setSuccess(null);

    if (!editCenterForm.name.trim()) {
      setError(t('centerManagement.errors.centerNameRequired'));
      return;
    }

    const payload: HospitalPayload = {
      name: editCenterForm.name.trim(),
      street: editCenterForm.street.trim() || undefined,
      city: editCenterForm.city.trim() || undefined,
      postalCode: editCenterForm.postalCode.trim() || undefined,
      country: editCenterCountryCode,
      countryScopeId: countryScopeByCode[editCenterCountryCode] ?? 203
    };

    try {
      await apiClient.put(`/api/hospitals/${selectedHospitalId}`, payload);
      setSuccess(t('centerManagement.messages.centerUpdated'));
      await loadHospitalsAndDoctors();
    } catch (updateError) {
      console.error('Error updating center', updateError);
      setError(t('centerManagement.errors.updateCenter'));
    }
  };

  const handleDeactivateCenter = async () => {
    if (!selectedHospitalId) {
      return;
    }

    if (!window.confirm(t('centerManagement.confirmDeactivateCenter'))) {
      return;
    }

    setError(null);
    setSuccess(null);

    try {
      await apiClient.delete(`/api/hospitals/${selectedHospitalId}`);
      setSuccess(t('centerManagement.messages.centerDeactivated'));
      setSelectedHospitalId(null);
      await loadHospitalsAndDoctors();
    } catch (deactivateError) {
      console.error('Error deactivating center', deactivateError);
      setError(t('centerManagement.errors.deactivateCenter'));
    }
  };

  const handleCreateRoom = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!selectedHospitalId) {
      return;
    }

    if (!newRoomName.trim()) {
      setError(t('centerManagement.errors.roomNameRequired'));
      return;
    }

    setError(null);
    setSuccess(null);

    try {
      await apiClient.post('/api/examinationrooms', {
        name: newRoomName.trim(),
        description: newRoomDescription.trim() || undefined,
        hospitalId: selectedHospitalId
      });
      setNewRoomName('');
      setNewRoomDescription('');
      setSuccess(t('centerManagement.messages.roomCreated'));
      await loadSelectedCenterData();
    } catch (createError) {
      console.error('Error creating room', createError);
      setError(t('centerManagement.errors.createRoom'));
    }
  };

  const handleAssignDoctor = async (event: React.FormEvent) => {
    event.preventDefault();

    if (!selectedDoctorId || !selectedRoomId) {
      setError(t('centerManagement.errors.assignSelectionRequired'));
      return;
    }

    setError(null);
    setSuccess(null);

    try {
      await apiClient.post('/api/doctorexaminationrooms', {
        doctorId: selectedDoctorId,
        examinationRoomId: selectedRoomId
      });
      setSelectedDoctorId(null);
      setSelectedRoomId(null);
      setSuccess(t('centerManagement.messages.doctorAssigned'));
      await loadAssignments();
    } catch (assignError) {
      console.error('Error assigning doctor', assignError);
      setError(t('centerManagement.errors.assignDoctor'));
    }
  };

  const handleRemoveAssignment = async (assignmentId: number) => {
    if (!window.confirm(t('centerManagement.confirmRemoveAssignment'))) {
      return;
    }

    setError(null);
    setSuccess(null);

    try {
      await apiClient.delete(`/api/doctorexaminationrooms/${assignmentId}`);
      setSuccess(t('centerManagement.messages.assignmentRemoved'));
      await loadAssignments();
    } catch (removeError) {
      console.error('Error removing doctor assignment', removeError);
      setError(t('centerManagement.errors.removeAssignment'));
    }
  };

  const toggleAllowedType = (typeId: number) => {
    setAllowedExaminationTypeIds((current) => {
      if (current.includes(typeId)) {
        return current.filter((id) => id !== typeId);
      }

      return [...current, typeId];
    });
  };

  const handleSaveAllowedExaminationTypes = async () => {
    if (!selectedHospitalId) {
      return;
    }

    setError(null);
    setSuccess(null);
    setSavingAllowedTypes(true);

    try {
      const payload = [...allowedExaminationTypeIds].sort((left, right) => left - right);
      await apiClient.put(`/api/hospitals/${selectedHospitalId}/examination-types`, payload);
      setSuccess(t('centerManagement.messages.allowedExaminationsSaved'));
    } catch (saveError) {
      console.error('Error saving allowed examination types', saveError);
      setError(t('centerManagement.errors.saveAllowedExaminations'));
    } finally {
      setSavingAllowedTypes(false);
    }
  };

  if (!canManageCenters) {
    return (
      <div className="center-management-page">
        <AppHeader sectionTitle={t('centerManagement.title')} />
        <div className="center-management-layout">
          <div className="panel">
            <h3>{t('centerManagement.noAccessTitle')}</h3>
            <p>{t('centerManagement.noAccessDescription')}</p>
          </div>
        </div>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="center-management-page">
        <AppHeader sectionTitle={t('centerManagement.title')} />
        <div className="center-management-layout">
          <div className="panel">{t('common.loading')}</div>
        </div>
      </div>
    );
  }

  return (
    <div className="center-management-page">
      <AppHeader sectionTitle={t('centerManagement.title')} />

      <div className="center-management-layout">
        {error && <div className="alert alert-error">{error}</div>}
        {success && <div className="alert alert-success">{success}</div>}

        <div className="hero-panel">
          <div>
            <p className="eyebrow">{t('centerManagement.eyebrow')}</p>
            <h2>{t('centerManagement.heroTitle')}</h2>
            <p>{t('centerManagement.heroDescription')}</p>
          </div>
          <div className="hero-controls">
            <label>
              <span>{t('centerManagement.selectedCenter')}</span>
              <select
                value={selectedHospitalId ?? ''}
                onChange={(event) => setSelectedHospitalId(event.target.value ? Number(event.target.value) : null)}
              >
                <option value="">{t('centerManagement.selectCenterPlaceholder')}</option>
                {hospitals.map((hospital) => (
                  <option key={hospital.id} value={hospital.id}>
                    {getHospitalTitle(hospital)}
                  </option>
                ))}
              </select>
            </label>
            <button type="button" className="secondary-button" onClick={refreshAll}>
              {t('centerManagement.refreshButton')}
            </button>
          </div>
        </div>

        <div className="grid two-columns">
          <section className="panel">
            <h3>{t('centerManagement.createCenterTitle')}</h3>
            <form className="form-grid" onSubmit={handleCreateCenter}>
              <label>
                <span>{t('centerManagement.centerName')}</span>
                <input
                  value={createCenterForm.name}
                  onChange={(event) => setCreateCenterForm((current) => ({ ...current, name: event.target.value }))}
                  placeholder={t('centerManagement.centerNamePlaceholder')}
                />
              </label>
              <label>
                <span>{t('centerManagement.street')}</span>
                <input
                  value={createCenterForm.street}
                  onChange={(event) => setCreateCenterForm((current) => ({ ...current, street: event.target.value }))}
                />
              </label>
              <label>
                <span>{t('centerManagement.city')}</span>
                <input
                  value={createCenterForm.city}
                  onChange={(event) => setCreateCenterForm((current) => ({ ...current, city: event.target.value }))}
                />
              </label>
              <label>
                <span>{t('centerManagement.postalCode')}</span>
                <input
                  value={createCenterForm.postalCode}
                  onChange={(event) => setCreateCenterForm((current) => ({ ...current, postalCode: event.target.value }))}
                />
              </label>
              <label>
                <span>{t('centerManagement.countryCode')}</span>
                <select
                  value={createCenterCountryCode}
                  onChange={(event) => setCreateCenterCountryCode(event.target.value)}
                  disabled={isRestrictedCountryAdmin}
                >
                  {countryOptions.map((countryCode) => (
                    <option key={countryCode} value={countryCode}>
                      {countryCode}
                    </option>
                  ))}
                </select>
              </label>
              <button type="submit" className="primary-button">{t('centerManagement.createCenterButton')}</button>
            </form>
          </section>

          <section className="panel">
            <h3>{t('centerManagement.editCenterTitle')}</h3>
            {selectedHospital ? (
              <>
                <p className="subtle-text">{getHospitalAddress(selectedHospital) || t('centerManagement.noAddress')}</p>
                <form className="form-grid" onSubmit={handleUpdateCenter}>
                  <label>
                    <span>{t('centerManagement.centerName')}</span>
                    <input
                      value={editCenterForm.name}
                      onChange={(event) => setEditCenterForm((current) => ({ ...current, name: event.target.value }))}
                    />
                  </label>
                  <label>
                    <span>{t('centerManagement.street')}</span>
                    <input
                      value={editCenterForm.street}
                      onChange={(event) => setEditCenterForm((current) => ({ ...current, street: event.target.value }))}
                    />
                  </label>
                  <label>
                    <span>{t('centerManagement.city')}</span>
                    <input
                      value={editCenterForm.city}
                      onChange={(event) => setEditCenterForm((current) => ({ ...current, city: event.target.value }))}
                    />
                  </label>
                  <label>
                    <span>{t('centerManagement.postalCode')}</span>
                    <input
                      value={editCenterForm.postalCode}
                      onChange={(event) => setEditCenterForm((current) => ({ ...current, postalCode: event.target.value }))}
                    />
                  </label>
                  <label>
                    <span>{t('centerManagement.countryCode')}</span>
                    <select
                      value={editCenterCountryCode}
                      onChange={(event) => setEditCenterCountryCode(event.target.value)}
                      disabled={isRestrictedCountryAdmin}
                    >
                      {countryOptions.map((countryCode) => (
                        <option key={countryCode} value={countryCode}>
                          {countryCode}
                        </option>
                      ))}
                    </select>
                  </label>
                  <div className="button-row">
                    <button type="submit" className="primary-button">{t('centerManagement.updateCenterButton')}</button>
                    <button type="button" className="danger-button" onClick={handleDeactivateCenter}>
                      {t('centerManagement.deactivateCenterButton')}
                    </button>
                  </div>
                </form>
              </>
            ) : (
              <p>{t('centerManagement.selectCenterFirst')}</p>
            )}
          </section>
        </div>

        <div className="grid two-columns">
          <section className="panel">
            <h3>{t('centerManagement.roomsTitle')}</h3>
            <form className="form-grid" onSubmit={handleCreateRoom}>
              <label>
                <span>{t('centerManagement.roomName')}</span>
                <input value={newRoomName} onChange={(event) => setNewRoomName(event.target.value)} />
              </label>
              <label>
                <span>{t('centerManagement.roomDescription')}</span>
                <input value={newRoomDescription} onChange={(event) => setNewRoomDescription(event.target.value)} />
              </label>
              <button type="submit" className="primary-button" disabled={!selectedHospitalId}>
                {t('centerManagement.createRoomButton')}
              </button>
            </form>

            <ul className="list">
              {rooms.map((room) => (
                <li key={room.id}>
                  <strong>{room.name}</strong>
                  <span>{room.description || t('centerManagement.noDescription')}</span>
                </li>
              ))}
              {rooms.length === 0 && <li>{t('centerManagement.noRooms')}</li>}
            </ul>
          </section>

          <section className="panel">
            <h3>{t('centerManagement.assignPersonnelTitle')}</h3>
            <form className="form-grid" onSubmit={handleAssignDoctor}>
              <label>
                <span>{t('centerManagement.selectPersonnel')}</span>
                <select
                  value={selectedDoctorId ?? ''}
                  onChange={(event) => setSelectedDoctorId(event.target.value ? Number(event.target.value) : null)}
                >
                  <option value="">{t('centerManagement.selectPersonnelPlaceholder')}</option>
                  {personnel.map((person) => (
                    <option key={person.employeeId} value={person.employeeId}>
                      {person.fullName}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <span>{t('centerManagement.selectRoom')}</span>
                <select
                  value={selectedRoomId ?? ''}
                  onChange={(event) => setSelectedRoomId(event.target.value ? Number(event.target.value) : null)}
                >
                  <option value="">{t('centerManagement.selectRoomPlaceholder')}</option>
                  {rooms.map((room) => (
                    <option key={room.id} value={room.id}>
                      {room.name}
                    </option>
                  ))}
                </select>
              </label>
              <button type="submit" className="primary-button" disabled={!selectedHospitalId || rooms.length === 0}>
                {t('centerManagement.assignPersonnelButton')}
              </button>
            </form>

            <div className="assignment-list">
              {assignments.map((assignment) => (
                <div key={assignment.id} className="assignment-item">
                  <div>
                    <strong>{assignment.roomName}</strong>
                    <span>{assignment.doctorName}</span>
                  </div>
                  <button type="button" className="danger-button" onClick={() => handleRemoveAssignment(assignment.id)}>
                    {t('centerManagement.removeAssignmentButton')}
                  </button>
                </div>
              ))}
              {assignments.length === 0 && <p>{t('centerManagement.noAssignments')}</p>}
            </div>
          </section>
        </div>

        <section className="panel">
          <h3>{t('centerManagement.allowedExaminationsTitle')}</h3>
          <p className="subtle-text">{t('centerManagement.allowedExaminationsDescription')}</p>

          <div className="types-grid">
            {allExaminationTypes.map((type) => (
              <label key={type.id} className="checkbox-row">
                <input
                  type="checkbox"
                  checked={allowedExaminationTypeIds.includes(type.id)}
                  onChange={() => toggleAllowedType(type.id)}
                  disabled={!selectedHospitalId}
                />
                <span>{type.name}</span>
              </label>
            ))}
          </div>

          <div className="button-row">
            <button
              type="button"
              className="primary-button"
              onClick={handleSaveAllowedExaminationTypes}
              disabled={!selectedHospitalId || savingAllowedTypes}
            >
              {savingAllowedTypes ? t('centerManagement.savingAllowedExaminations') : t('centerManagement.saveAllowedExaminationsButton')}
            </button>
          </div>
        </section>
      </div>
    </div>
  );
};

export default CenterManagementPage;
