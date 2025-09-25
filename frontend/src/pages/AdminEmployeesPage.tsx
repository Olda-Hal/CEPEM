import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import './AdminEmployeesPage.css';
import { EmployeeListItem, Role, UpdateEmployeeRequest, UpdateEmployeeResponse } from '../types';
import { apiClient } from '../utils/api';
import { AppHeader } from '../components/AppHeader';

const AdminEmployeesPage: React.FC = () => {
  const { t } = useTranslation();
  const [employees, setEmployees] = useState<EmployeeListItem[]>([]);
  const [roles, setRoles] = useState<Role[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedEmployee, setSelectedEmployee] = useState<EmployeeListItem | null>(null);
  const [showEditModal, setShowEditModal] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      const [employeesResponse, rolesResponse] = await Promise.all([
        apiClient.get<EmployeeListItem[]>('/api/admin/employees'),
        apiClient.get<Role[]>('/api/admin/roles')
      ]);
      setEmployees(employeesResponse);
      setRoles(rolesResponse);
    } catch (error) {
      console.error('Error loading data:', error);
      setError('Failed to load data');
    } finally {
      setLoading(false);
    }
  };

  const handleEdit = (employee: EmployeeListItem) => {
    setSelectedEmployee(employee);
    setShowEditModal(true);
    setError(null);
    setSuccess(null);
  };

  const handleDeactivate = async (employeeId: number) => {
    if (!window.confirm(t('admin.confirmDeactivate'))) {
      return;
    }

    try {
      await apiClient.patch(`/api/admin/employees/${employeeId}/deactivate`);
      setSuccess('Employee deactivated successfully');
      await loadData();
    } catch (error) {
      console.error('Error deactivating employee:', error);
      setError('Failed to deactivate employee');
    }
  };

  const handleSave = async (updatedEmployee: UpdateEmployeeRequest) => {
    if (!selectedEmployee) return;

    try {
      const response = await apiClient.put<UpdateEmployeeResponse>(`/api/admin/employees/${selectedEmployee.employeeId}`, updatedEmployee);
      if (response.success) {
        setSuccess(response.message);
        setShowEditModal(false);
        setSelectedEmployee(null);
        await loadData();
      } else {
        setError(response.message);
      }
    } catch (error) {
      console.error('Error updating employee:', error);
      setError('Failed to update employee');
    }
  };

  if (loading) {
    return (
      <div className="admin-employees-page">
        <div className="loading">Loading...</div>
      </div>
    );
  }

  return (
    <div className="admin-employees-page">
      <AppHeader sectionTitle={t('admin.employeeManagement')} />

      {error && <div className="alert alert-error">{error}</div>}
      {success && <div className="alert alert-success">{success}</div>}

      <div className="employees-table-container">
        <table className="employees-table">
          <thead>
            <tr>
              <th>{t('admin.fullName')}</th>
              <th>{t('admin.email')}</th>
              <th>{t('admin.uid')}</th>
              <th>{t('admin.phone')}</th>
              <th>{t('admin.roles')}</th>
              <th>{t('admin.status')}</th>
              <th>{t('admin.lastLogin')}</th>
              <th>{t('admin.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {employees.map((employee) => (
              <tr key={employee.employeeId} className={!employee.active ? 'inactive' : ''}>
                <td>
                  <div className="employee-name">
                    <strong>{employee.fullName}</strong>
                    <small>{employee.gender}</small>
                  </div>
                </td>
                <td>{employee.email}</td>
                <td>{employee.uid}</td>
                <td>{employee.phoneNumber}</td>
                <td>
                  <div className="roles">
                    {employee.roles.map((role, index) => (
                      <span key={index} className="role-badge">{role}</span>
                    ))}
                  </div>
                </td>
                <td>
                  <span className={`status-badge ${employee.active ? 'active' : 'inactive'}`}>
                    {employee.active ? t('admin.active') : t('admin.inactive')}
                  </span>
                </td>
                <td>
                  {employee.lastLoginAt 
                    ? new Date(employee.lastLoginAt).toLocaleString()
                    : t('admin.never')
                  }
                </td>
                <td>
                  <div className="action-buttons">
                    <button 
                      className="btn btn-primary btn-sm"
                      onClick={() => handleEdit(employee)}
                    >
                      {t('admin.edit')}
                    </button>
                    {employee.active && (
                      <button 
                        className="btn btn-danger btn-sm"
                        onClick={() => handleDeactivate(employee.employeeId)}
                      >
                        {t('admin.deactivate')}
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showEditModal && selectedEmployee && (
        <EmployeeEditModal
          employee={selectedEmployee}
          roles={roles}
          onSave={handleSave}
          onCancel={() => {
            setShowEditModal(false);
            setSelectedEmployee(null);
            setError(null);
          }}
        />
      )}
    </div>
  );
};

interface EmployeeEditModalProps {
  employee: EmployeeListItem;
  roles: Role[];
  onSave: (employee: UpdateEmployeeRequest) => void;
  onCancel: () => void;
}

const EmployeeEditModal: React.FC<EmployeeEditModalProps> = ({ employee, roles, onSave, onCancel }) => {
  const { t } = useTranslation();
  const [formData, setFormData] = useState<UpdateEmployeeRequest>({
    firstName: employee.firstName,
    lastName: employee.lastName,
    email: employee.email,
    phoneNumber: employee.phoneNumber,
    uid: employee.uid,
    gender: employee.gender,
    titleBefore: employee.titleBefore || '',
    titleAfter: employee.titleAfter || '',
    active: employee.active,
    roleIds: []
  });

  useEffect(() => {
    // Set initial role IDs based on employee's current roles
    const currentRoleIds = roles
      .filter(role => employee.roles.includes(role.name))
      .map(role => role.id);
    setFormData(prev => ({ ...prev, roleIds: currentRoleIds }));
  }, [employee.roles, roles]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSave(formData);
  };

  const handleRoleChange = (roleId: number, checked: boolean) => {
    setFormData(prev => ({
      ...prev,
      roleIds: checked 
        ? [...prev.roleIds, roleId]
        : prev.roleIds.filter(id => id !== roleId)
    }));
  };

  return (
    <div className="modal-overlay">
      <div className="modal-content">
        <div className="modal-header">
          <h2>{t('admin.editEmployee')}</h2>
          <button className="close-button" onClick={onCancel}>&times;</button>
        </div>
        
        <form onSubmit={handleSubmit} className="employee-form">
          <div className="form-row">
            <div className="form-group">
              <label>{t('admin.titleBefore')}</label>
              <input
                type="text"
                value={formData.titleBefore}
                onChange={(e) => setFormData(prev => ({ ...prev, titleBefore: e.target.value }))}
              />
            </div>
            <div className="form-group">
              <label>{t('admin.firstName')} *</label>
              <input
                type="text"
                value={formData.firstName}
                onChange={(e) => setFormData(prev => ({ ...prev, firstName: e.target.value }))}
                required
              />
            </div>
            <div className="form-group">
              <label>{t('admin.lastName')} *</label>
              <input
                type="text"
                value={formData.lastName}
                onChange={(e) => setFormData(prev => ({ ...prev, lastName: e.target.value }))}
                required
              />
            </div>
            <div className="form-group">
              <label>{t('admin.titleAfter')}</label>
              <input
                type="text"
                value={formData.titleAfter}
                onChange={(e) => setFormData(prev => ({ ...prev, titleAfter: e.target.value }))}
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label>{t('admin.email')} *</label>
              <input
                type="email"
                value={formData.email}
                onChange={(e) => setFormData(prev => ({ ...prev, email: e.target.value }))}
                required
              />
            </div>
            <div className="form-group">
              <label>{t('admin.phone')} *</label>
              <input
                type="tel"
                value={formData.phoneNumber}
                onChange={(e) => setFormData(prev => ({ ...prev, phoneNumber: e.target.value }))}
                required
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label>{t('admin.uid')} *</label>
              <input
                type="text"
                value={formData.uid}
                onChange={(e) => setFormData(prev => ({ ...prev, uid: e.target.value }))}
                required
              />
            </div>
            <div className="form-group">
              <label>{t('admin.gender')} *</label>
              <select
                value={formData.gender}
                onChange={(e) => setFormData(prev => ({ ...prev, gender: e.target.value }))}
                required
              >
                <option value="Male">{t('admin.male')}</option>
                <option value="Female">{t('admin.female')}</option>
                <option value="Other">{t('admin.other')}</option>
              </select>
            </div>
          </div>

          <div className="form-group">
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={formData.active}
                onChange={(e) => setFormData(prev => ({ ...prev, active: e.target.checked }))}
              />
              {t('admin.active')}
            </label>
          </div>

          <div className="form-group">
            <label>{t('admin.roles')}</label>
            <div className="roles-checkboxes">
              {roles.map((role) => (
                <label key={role.id} className="checkbox-label">
                  <input
                    type="checkbox"
                    checked={formData.roleIds.includes(role.id)}
                    onChange={(e) => handleRoleChange(role.id, e.target.checked)}
                  />
                  {role.name}
                </label>
              ))}
            </div>
          </div>

          <div className="modal-actions">
            <button type="button" className="btn btn-secondary" onClick={onCancel}>
              {t('admin.cancel')}
            </button>
            <button type="submit" className="btn btn-primary">
              {t('admin.save')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default AdminEmployeesPage;
