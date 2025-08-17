import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { CreateEmployeeRequest } from '../types';
import { apiClient } from '../utils/api';
import './CreateEmployeeModal.css';

interface CreateEmployeeModalProps {
  isOpen: boolean;
  onClose: () => void;
  onEmployeeCreated: () => void;
}

export const CreateEmployeeModal: React.FC<CreateEmployeeModalProps> = ({
  isOpen,
  onClose,
  onEmployeeCreated,
}) => {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(false);
  const [loadingUid, setLoadingUid] = useState(false);
  const [error, setError] = useState<string>('');
  const [formData, setFormData] = useState<CreateEmployeeRequest>({
    firstName: '',
    lastName: '',
    email: '',
    phoneNumber: '',
    uid: '',
    gender: '',
    password: '',
    titleBefore: '',
    titleAfter: '',
    active: true,
  });

  // Fetch next available UID when modal opens
  useEffect(() => {
    if (isOpen && !formData.uid) {
      fetchNextUid();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen]);

  const fetchNextUid = async () => {
    setLoadingUid(true);
    try {
      const nextUid = await apiClient.get<string>('/api/auth/next-uid');
      setFormData(prev => ({ ...prev, uid: nextUid }));
    } catch (err) {
      console.error('Failed to fetch next UID:', err);
      // Don't show error to user, just leave UID empty
    } finally {
      setLoadingUid(false);
    }
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value, type } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: type === 'checkbox' ? (e.target as HTMLInputElement).checked : value,
    }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');

    try {
      await apiClient.post('/api/auth/create-employee', formData);
      onEmployeeCreated();
      onClose();
      resetForm();
    } catch (err: any) {
      setError(err.response?.data || t('createEmployee.error'));
    } finally {
      setLoading(false);
    }
  };

  const resetForm = () => {
    setFormData({
      firstName: '',
      lastName: '',
      email: '',
      phoneNumber: '',
      uid: '',
      gender: '',
      password: '',
      titleBefore: '',
      titleAfter: '',
      active: true,
    });
    setError('');
  };

  const handleClose = () => {
    resetForm();
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="modal-overlay" onClick={handleClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>{t('createEmployee.title')}</h2>
          <button className="modal-close" onClick={handleClose}>
            ×
          </button>
        </div>

        <form onSubmit={handleSubmit} className="create-employee-form">
          {error && <div className="error-message">{error}</div>}

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="titleBefore">{t('createEmployee.titleBefore')}</label>
              <input
                type="text"
                id="titleBefore"
                name="titleBefore"
                value={formData.titleBefore}
                onChange={handleInputChange}
                placeholder={t('createEmployee.titleBeforePlaceholder')}
              />
            </div>
            <div className="form-group">
              <label htmlFor="titleAfter">{t('createEmployee.titleAfter')}</label>
              <input
                type="text"
                id="titleAfter"
                name="titleAfter"
                value={formData.titleAfter}
                onChange={handleInputChange}
                placeholder={t('createEmployee.titleAfterPlaceholder')}
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="firstName">{t('createEmployee.firstName')} *</label>
              <input
                type="text"
                id="firstName"
                name="firstName"
                value={formData.firstName}
                onChange={handleInputChange}
                required
              />
            </div>
            <div className="form-group">
              <label htmlFor="lastName">{t('createEmployee.lastName')} *</label>
              <input
                type="text"
                id="lastName"
                name="lastName"
                value={formData.lastName}
                onChange={handleInputChange}
                required
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="email">{t('createEmployee.email')} *</label>
              <input
                type="email"
                id="email"
                name="email"
                value={formData.email}
                onChange={handleInputChange}
                required
              />
            </div>
            <div className="form-group">
              <label htmlFor="phoneNumber">{t('createEmployee.phoneNumber')} *</label>
              <input
                type="tel"
                id="phoneNumber"
                name="phoneNumber"
                value={formData.phoneNumber}
                onChange={handleInputChange}
                required
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="uid">{t('createEmployee.uid')} *</label>
              <div className="uid-input-group">
                <input
                  type="text"
                  id="uid"
                  name="uid"
                  value={formData.uid}
                  onChange={handleInputChange}
                  required
                  disabled={loadingUid}
                  placeholder={loadingUid ? t('createEmployee.loadingUid') : ''}
                />
                <button
                  type="button"
                  className="refresh-uid-button"
                  onClick={fetchNextUid}
                  disabled={loadingUid}
                  title={t('createEmployee.refreshUid')}
                >
                  🔄
                </button>
              </div>
            </div>
            <div className="form-group">
              <label htmlFor="gender">{t('createEmployee.gender')} *</label>
              <select
                id="gender"
                name="gender"
                value={formData.gender}
                onChange={handleInputChange}
                required
              >
                <option value="">{t('createEmployee.selectGender')}</option>
                <option value="M">{t('createEmployee.male')}</option>
                <option value="F">{t('createEmployee.female')}</option>
              </select>
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="password">{t('createEmployee.password')} *</label>
            <input
              type="password"
              id="password"
              name="password"
              value={formData.password}
              onChange={handleInputChange}
              required
              minLength={6}
            />
            <small className="form-hint">{t('createEmployee.passwordHint')}</small>
          </div>

          <div className="form-group checkbox-group">
            <label>
              <input
                type="checkbox"
                name="active"
                checked={formData.active}
                onChange={handleInputChange}
              />
              {t('createEmployee.active')}
            </label>
          </div>

          <div className="modal-footer">
            <button
              type="button"
              onClick={handleClose}
              className="cancel-button"
              disabled={loading}
            >
              {t('createEmployee.cancel')}
            </button>
            <button
              type="submit"
              className="submit-button"
              disabled={loading}
            >
              {loading ? t('createEmployee.creating') : t('createEmployee.create')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
