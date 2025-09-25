import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { PatientDetail } from '../types';
import { apiClient } from '../utils/api';
import { AppHeader } from '../components/AppHeader';
import './PatientDetailPage.css';

export const PatientDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const [patient, setPatient] = useState<PatientDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (id) {
      loadPatientDetail(parseInt(id));
    }
  }, [id]);

  const loadPatientDetail = async (patientId: number) => {
    try {
      setLoading(true);
      const response = await apiClient.get<PatientDetail>(`/api/patients/${patientId}/detail`);
      setPatient(response);
      setError(null);
    } catch (error) {
      console.error('Error loading patient detail:', error);
      setError(t('errors.loadingPatientDetail'));
    } finally {
      setLoading(false);
    }
  };

  const handleBackToPatients = () => {
    navigate('/patients');
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString(i18n.language, {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  };

  const formatDateTime = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString(i18n.language, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  if (loading) {
    return (
      <div className="patient-detail-page">
        <AppHeader sectionTitle={t('patients.patientDetail')} />
        <div className="loading-container">
          <div className="loading-spinner"></div>
          <p>{t('common.loading')}</p>
        </div>
      </div>
    );
  }

  if (error || !patient) {
    return (
      <div className="patient-detail-page">
        <AppHeader sectionTitle={t('patients.patientDetail')} />
        <div className="error-container">
          <p className="error-message">{error || t('errors.patientNotFound')}</p>
          <button onClick={handleBackToPatients} className="back-button">
            {t('common.back')}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="patient-detail-page">
      <AppHeader sectionTitle={t('patients.patientDetail')}>
        <button onClick={handleBackToPatients} className="back-button">
          {t('common.backToPatients')}
        </button>
      </AppHeader>

      <div className="patient-detail-content">
        {/* Patient Basic Info */}
        <div className="patient-info-card">
          <div className="patient-header">
            <div className="patient-photo-placeholder">
              <svg className="photo-icon" viewBox="0 0 24 24">
                <path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/>
              </svg>
            </div>
            <div className="patient-basic-info">
              <h1 className="patient-name">
                {patient.titleBefore && <span className="title">{patient.titleBefore} </span>}
                {patient.fullName}
                {patient.titleAfter && <span className="title"> {patient.titleAfter}</span>}
              </h1>
              <div className="patient-details">
                <div className="detail-row">
                  <span className="label">{t('patients.fields.uid')}:</span>
                  <span className="value">{patient.uid}</span>
                </div>
                <div className="detail-row">
                  <span className="label">{t('patients.fields.birthDate')}:</span>
                  <span className="value">{formatDate(patient.birthDate)} ({patient.age} {t('patients.yearsOld')})</span>
                </div>
                <div className="detail-row">
                  <span className="label">{t('patients.fields.gender')}:</span>
                  <span className="value">{t(`patients.genders.${patient.gender.toLowerCase()}`)}</span>
                </div>
                <div className="detail-row">
                  <span className="label">{t('patients.fields.insuranceNumber')}:</span>
                  <span className="value">{patient.insuranceNumber}</span>
                </div>
                <div className="detail-row">
                  <span className="label">{t('patients.fields.email')}:</span>
                  <span className="value">{patient.email || '-'}</span>
                </div>
                <div className="detail-row">
                  <span className="label">{t('patients.fields.phoneNumber')}:</span>
                  <span className="value">{patient.phoneNumber || '-'}</span>
                </div>
                <div className="detail-row">
                  <span className="label">{t('patients.status')}:</span>
                  <span className={`value status ${patient.alive ? 'alive' : 'deceased'}`}>
                    {patient.alive ? t('patients.alive') : t('patients.deceased')}
                  </span>
                </div>
              </div>
            </div>
          </div>
          
          {patient.comment && (
            <div className="patient-comment">
              <h3>{t('patients.comment')}</h3>
              <p>{patient.comment}</p>
            </div>
          )}
        </div>

        {/* Patient Events */}
        <div className="patient-events-card">
          <div className="card-header">
            <h2>{t('patients.events')} ({patient.events.length})</h2>
            <button className="add-event-btn">
              {t('patients.addEvent')}
            </button>
          </div>
          
          {patient.events.length === 0 ? (
            <div className="no-data">
              <p>{t('patients.noEvents')}</p>
            </div>
          ) : (
            <div className="events-list">
              {patient.events.map((event) => (
                <div key={event.id} className="event-item">
                  <div className="event-header">
                    <h3 className="event-type">{event.eventTypeName}</h3>
                    <span className="event-date">{formatDateTime(event.happenedAt)}</span>
                  </div>
                  
                  {event.happenedTo && (
                    <div className="event-duration">
                      {t('patients.eventUntil')}: {formatDateTime(event.happenedTo)}
                    </div>
                  )}
                  
                  {event.comment && (
                    <div className="event-comment">
                      <p>{event.comment}</p>
                    </div>
                  )}
                  
                  <div className="event-details">
                    {event.drugUses.length > 0 && (
                      <div className="detail-section">
                        <h4>{t('patients.drugUses')}:</h4>
                        <ul>
                          {event.drugUses.map((drug, index) => (
                            <li key={index}>{drug}</li>
                          ))}
                        </ul>
                      </div>
                    )}
                    
                    {event.examinations.length > 0 && (
                      <div className="detail-section">
                        <h4>{t('patients.examinations')}:</h4>
                        <ul>
                          {event.examinations.map((exam, index) => (
                            <li key={index}>{exam}</li>
                          ))}
                        </ul>
                      </div>
                    )}
                    
                    {event.symptoms.length > 0 && (
                      <div className="detail-section">
                        <h4>{t('patients.symptoms')}:</h4>
                        <ul>
                          {event.symptoms.map((symptom, index) => (
                            <li key={index}>{symptom}</li>
                          ))}
                        </ul>
                      </div>
                    )}
                    
                    {event.injuries.length > 0 && (
                      <div className="detail-section">
                        <h4>{t('patients.injuries')}:</h4>
                        <ul>
                          {event.injuries.map((injury, index) => (
                            <li key={index}>{injury}</li>
                          ))}
                        </ul>
                      </div>
                    )}
                    
                    {event.vaccines.length > 0 && (
                      <div className="detail-section">
                        <h4>{t('patients.vaccines')}:</h4>
                        <ul>
                          {event.vaccines.map((vaccine, index) => (
                            <li key={index}>{vaccine}</li>
                          ))}
                        </ul>
                      </div>
                    )}
                    
                    {event.hasPregnancy && (
                      <div className="detail-section">
                        <h4>{t('patients.pregnancy')}</h4>
                      </div>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Patient Appointments */}
        <div className="patient-appointments-card">
          <div className="card-header">
            <h2>{t('patients.appointments')} ({patient.appointments.length})</h2>
            <button className="add-appointment-btn">
              {t('patients.addAppointment')}
            </button>
          </div>
          
          {patient.appointments.length === 0 ? (
            <div className="no-data">
              <p>{t('patients.noAppointments')}</p>
            </div>
          ) : (
            <div className="appointments-list">
              {patient.appointments.map((appointment) => (
                <div key={appointment.id} className="appointment-item">
                  <div className="appointment-time">
                    <div className="start-time">{formatDateTime(appointment.startTime)}</div>
                    <div className="end-time">{formatDateTime(appointment.endTime)}</div>
                  </div>
                  
                  <div className="appointment-details">
                    <div className="doctor">{appointment.doctorName}</div>
                    <div className="hospital">{appointment.hospitalName}</div>
                    {appointment.equipmentName && (
                      <div className="equipment">{appointment.equipmentName}</div>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
