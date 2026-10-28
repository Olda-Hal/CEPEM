import React from 'react';
import { useTranslation } from 'react-i18next';
import { PatientDetail } from '../../types';
import { formatDateTime, formatFileSize } from '../../utils/format';

interface PatientEventsSectionProps {
  events: PatientDetail['events'];
  expandedEvents: Set<number>;
  onToggleEvent: (eventId: number) => void;
  onAddEvent: () => void;
  onAddIntakeForm: () => void;
  onGenerateIntakeLink: () => void;
  onExaminationDocumentView: (examinationId: number, documentId: number) => void;
}

/** The patient events card on the detail page (event history list). */
export const PatientEventsSection: React.FC<PatientEventsSectionProps> = ({
  events,
  expandedEvents,
  onToggleEvent,
  onAddEvent,
  onAddIntakeForm,
  onGenerateIntakeLink,
  onExaminationDocumentView
}) => {
  const { t, i18n } = useTranslation();

  return (
    <div className="patient-events-card">
      <div className="card-header">
        <h2>
          {t('patients.events')} ({events.length})
        </h2>
        <div className="button-group">
          <button className="add-event-btn" onClick={onAddIntakeForm}>
            📋 Vstupní Formulář
          </button>
          <button className="add-event-btn" onClick={onGenerateIntakeLink}>
            {t('patients.generateIntakeLink')}
          </button>
          <button className="add-event-btn" onClick={onAddEvent}>
            {t('patients.addEvent')}
          </button>
        </div>
      </div>

      {events.length === 0 ? (
        <div className="no-data">
          <p>{t('patients.noEvents')}</p>
        </div>
      ) : (
        <div className="events-list">
          {events.map((event) => {
            const eventDocuments = event.examinations.flatMap((exam) =>
              exam.documents.map((doc) => ({
                examId: exam.id,
                examName: exam.name,
                ...doc
              }))
            );

            return (
              <div key={event.id} className="event-item">
                <button
                  className="event-header event-toggle"
                  onClick={() => onToggleEvent(event.id)}
                >
                  <div className="event-title-wrap">
                    <svg
                      className={`event-chevron ${expandedEvents.has(event.id) ? 'expanded' : ''}`}
                      viewBox="0 0 24 24"
                      width="16"
                      height="16"
                    >
                      <path
                        fill="currentColor"
                        d="M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z"
                      />
                    </svg>
                    <h3 className="event-type">{event.eventTypeName}</h3>
                  </div>
                  <span className="event-date">
                    {formatDateTime(event.happenedAt, i18n.language)}
                  </span>
                </button>

                {expandedEvents.has(event.id) && (
                  <div className="event-expanded-content">
                    {event.happenedTo && (
                      <div className="event-duration">
                        {t('patients.eventUntil')}:{' '}
                        {formatDateTime(event.happenedTo, i18n.language)}
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

                    {event.examinations.length > 0 && (
                      <div className="examinations-full-section">
                        <h4>{t('patients.examinations')}:</h4>
                        <ul className="examination-list">
                          {event.examinations.map((exam) => (
                            <li key={exam.id} className="examination-list-item">
                              <span className="examination-name">{exam.name}</span>
                              {exam.documents.length > 0 && (
                                <span className="examination-doc-badge">
                                  {exam.documents.length}
                                </span>
                              )}
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}

                    <div className="event-documents-section">
                      <h4>{t('patients.documents.title')}:</h4>
                      {eventDocuments.length === 0 ? (
                        <p className="no-exam-documents">{t('patients.documents.noDocuments')}</p>
                      ) : (
                        <ul className="event-document-list">
                          {eventDocuments.map((doc) => (
                            <li key={`${doc.examId}-${doc.id}`} className="event-document-item">
                              <button
                                className="event-document-btn"
                                onClick={() => onExaminationDocumentView(doc.examId, doc.id)}
                                title={doc.fileName}
                              >
                                <svg
                                  viewBox="0 0 24 24"
                                  width="18"
                                  height="18"
                                  className="event-doc-icon"
                                >
                                  <path
                                    fill="currentColor"
                                    d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zM6 20V4h7v5h5v11H6z"
                                  />
                                </svg>
                                <div className="event-doc-main">
                                  <span className="event-doc-file">{doc.fileName}</span>
                                  <span className="event-doc-exam">{doc.examName}</span>
                                </div>
                                <span className="event-doc-meta">
                                  {formatDateTime(doc.uploadedAt, i18n.language)} ·{' '}
                                  {formatFileSize(doc.fileSize)}
                                </span>
                              </button>
                            </li>
                          ))}
                        </ul>
                      )}
                    </div>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};

export default PatientEventsSection;
