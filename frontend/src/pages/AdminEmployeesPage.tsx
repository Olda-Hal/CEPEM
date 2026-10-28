import React, { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import './AdminEmployeesPage.css';
import { EmployeeListItem, Role, UpdateEmployeeRequest, UpdateEmployeeResponse } from '../types';
import { apiClient, getAuthToken } from '../utils/api';
import PermissionsModal from '../components/PermissionsModal';
import { AppHeader } from '../components/AppHeader';
import { useAuth } from '../contexts/AuthContext';
import { hasRole } from '../utils/roles';

const EMPLOYEES_PER_PAGE = 25;

const AdminEmployeesPage: React.FC = () => {
  const { t } = useTranslation();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [selectedEmployee, setSelectedEmployee] = useState<EmployeeListItem | null>(null);
  const [showEditModal, setShowEditModal] = useState(false);
  const [showRulesModal, setShowRulesModal] = useState(false);
  const [selectedRulesEmployee, setSelectedRulesEmployee] = useState<EmployeeListItem | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [page, setPage] = useState(0);

  const handleExport = useCallback(async () => {
    try {
      const token = getAuthToken();
      const response = await fetch('/api/admin/employees/export', {
        headers: token ? { Authorization: `Bearer ${token}` } : {}
      });
      if (!response.ok) throw new Error(`Export failed: ${response.status}`);
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download =
        response.headers.get('Content-Disposition')?.split('filename=')[1] ?? 'employees.csv';
      link.click();
      URL.revokeObjectURL(url);
    } catch (exportError) {
      console.error(exportError);
      setError(t('admin.dataLoadFailed'));
    }
  }, [t]);

  const { data, isLoading: loading } = useQuery({
    queryKey: ['admin', 'employees-and-roles'],
    queryFn: async () => {
      const [employees, roles] = await Promise.all([
        apiClient.get<EmployeeListItem[]>('/api/admin/employees'),
        apiClient.get<Role[]>('/api/admin/roles')
      ]);
      return { employees, roles };
    }
  });

  const loadData = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: ['admin', 'employees-and-roles'] });
  }, [queryClient]);

  const isRestrictedCountryAdmin =
    hasRole(user, 'Country Admin') && !hasRole(user, 'SysAdmin') && !hasRole(user, 'Admin');

  const canManageEmployee = (employee: EmployeeListItem) => {
    if (!isRestrictedCountryAdmin) {
      return true;
    }

    return employee.createdByEmployeeId === user?.id;
  };

  const translateRoleName = (roleName: string) => {
    const keyMap: Record<string, string> = {
      SysAdmin: 'admin.roleNames.sysAdmin',
      Examiner: 'admin.roleNames.examiner',
      Doctor: 'admin.roleNames.doctor',
      'Center Admin': 'admin.roleNames.centerAdmin',
      'Country Admin': 'admin.roleNames.countryAdmin'
    };

    const key = keyMap[roleName];
    return key ? t(key) : roleName;
  };

  const getPermissionLabel = (permissionKey: string, fallback?: string) => {
    const translationKey = `admin.permissionLabels.${permissionKey
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '_')
      .replace(/^_|_$/g, '')}`;
    return t(translationKey, { defaultValue: fallback || permissionKey });
  };

  const handleRules = (employee: EmployeeListItem) => {
    setSelectedRulesEmployee(employee);
    setShowRulesModal(true);
    setError(null);
    setSuccess(null);
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
      setSuccess(t('admin.employeeDeactivatedSuccess'));
      await loadData();
    } catch (error) {
      console.error('Error deactivating employee:', error);
      setError(t('admin.employeeDeactivateFailed'));
    }
  };

  const handleSave = async (updatedEmployee: UpdateEmployeeRequest) => {
    if (!selectedEmployee) return;

    try {
      const response = await apiClient.put<UpdateEmployeeResponse>(
        `/api/admin/employees/${selectedEmployee.employeeId}`,
        updatedEmployee
      );
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
      setError(t('admin.employeeUpdateFailed'));
    }
  };

  if (loading) {
    return (
      <div className="admin-employees-page">
        <div className="loading">{t('common.loading')}</div>
      </div>
    );
  }

  const employees = data?.employees ?? [];
  const roles = data?.roles ?? [];

  const totalPages = Math.max(1, Math.ceil(employees.length / EMPLOYEES_PER_PAGE));
  const pagedEmployees = employees.slice(
    page * EMPLOYEES_PER_PAGE,
    (page + 1) * EMPLOYEES_PER_PAGE
  );

  return (
    <div className="admin-employees-page">
      <AppHeader sectionTitle={t('admin.employeeManagement')}>
        <button className="btn btn-secondary" onClick={handleExport}>
          {t('activityLog.exportCsv')}
        </button>
      </AppHeader>

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
            {pagedEmployees.map((employee) => (
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
                      <span key={index} className="role-badge">
                        {translateRoleName(role)}
                      </span>
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
                    : t('admin.never')}
                </td>
                <td>
                  <div className="action-buttons">
                    <button
                      className="btn btn-primary btn-sm"
                      onClick={() => handleEdit(employee)}
                      disabled={!canManageEmployee(employee)}
                    >
                      {t('admin.edit')}
                    </button>
                    {!isRestrictedCountryAdmin && (
                      <button
                        className="btn btn-secondary btn-sm"
                        onClick={() => handleRules(employee)}
                      >
                        {t('admin.access')}
                      </button>
                    )}
                    {employee.active && (
                      <button
                        className="btn btn-danger btn-sm"
                        onClick={() => handleDeactivate(employee.employeeId)}
                        disabled={!canManageEmployee(employee)}
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
        {totalPages > 1 && (
          <div className="employees-pagination">
            <button
              className="btn btn-secondary"
              onClick={() => setPage((p) => Math.max(0, p - 1))}
              disabled={page === 0}
            >
              {t('common.previous')}
            </button>
            <span className="pagination-info">
              {page + 1} / {totalPages}
            </span>
            <button
              className="btn btn-secondary"
              onClick={() => setPage((p) => Math.min(totalPages - 1, p + 1))}
              disabled={page >= totalPages - 1}
            >
              {t('common.next')}
            </button>
          </div>
        )}
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

      {showRulesModal && selectedRulesEmployee && (
        <PermissionsModal
          employee={selectedRulesEmployee}
          onClose={() => {
            setShowRulesModal(false);
            setSelectedRulesEmployee(null);
          }}
          onSaved={() => {
            setSuccess(t('admin.accessSaved'));
          }}
          getPermissionLabel={getPermissionLabel}
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

const EmployeeEditModal: React.FC<EmployeeEditModalProps> = ({
  employee,
  roles,
  onSave,
  onCancel
}) => {
  const { t } = useTranslation();
  const translateRoleName = (roleName: string) => {
    const keyMap: Record<string, string> = {
      SysAdmin: 'admin.roleNames.sysAdmin',
      Examiner: 'admin.roleNames.examiner',
      Doctor: 'admin.roleNames.doctor',
      'Center Admin': 'admin.roleNames.centerAdmin',
      'Country Admin': 'admin.roleNames.countryAdmin'
    };

    const key = keyMap[roleName];
    return key ? t(key) : roleName;
  };

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
      .filter((role) => employee.roles.includes(role.name))
      .map((role) => role.id);
    setFormData((prev) => ({ ...prev, roleIds: currentRoleIds }));
  }, [employee.roles, roles]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSave(formData);
  };

  const handleRoleChange = (roleId: number, checked: boolean) => {
    setFormData((prev) => ({
      ...prev,
      roleIds: checked ? [...prev.roleIds, roleId] : prev.roleIds.filter((id) => id !== roleId)
    }));
  };

  return (
    <div className="modal-overlay">
      <div className="modal-content">
        <div className="modal-header">
          <h2>{t('admin.editEmployee')}</h2>
          <button className="close-button" onClick={onCancel}>
            &times;
          </button>
        </div>

        <form onSubmit={handleSubmit} className="employee-form">
          <div className="form-row">
            <div className="form-group">
              <label>{t('admin.titleBefore')}</label>
              <input
                type="text"
                value={formData.titleBefore}
                onChange={(e) => setFormData((prev) => ({ ...prev, titleBefore: e.target.value }))}
              />
            </div>
            <div className="form-group">
              <label>{t('admin.firstName')} *</label>
              <input
                type="text"
                value={formData.firstName}
                onChange={(e) => setFormData((prev) => ({ ...prev, firstName: e.target.value }))}
                required
              />
            </div>
            <div className="form-group">
              <label>{t('admin.lastName')} *</label>
              <input
                type="text"
                value={formData.lastName}
                onChange={(e) => setFormData((prev) => ({ ...prev, lastName: e.target.value }))}
                required
              />
            </div>
            <div className="form-group">
              <label>{t('admin.titleAfter')}</label>
              <input
                type="text"
                value={formData.titleAfter}
                onChange={(e) => setFormData((prev) => ({ ...prev, titleAfter: e.target.value }))}
              />
            </div>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label>{t('admin.email')} *</label>
              <input
                type="email"
                value={formData.email}
                onChange={(e) => setFormData((prev) => ({ ...prev, email: e.target.value }))}
                required
              />
            </div>
            <div className="form-group">
              <label>{t('admin.phone')} *</label>
              <input
                type="tel"
                value={formData.phoneNumber}
                onChange={(e) => setFormData((prev) => ({ ...prev, phoneNumber: e.target.value }))}
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
                onChange={(e) => setFormData((prev) => ({ ...prev, uid: e.target.value }))}
                required
              />
            </div>
            <div className="form-group">
              <label>{t('admin.gender')} *</label>
              <select
                value={formData.gender}
                onChange={(e) => setFormData((prev) => ({ ...prev, gender: e.target.value }))}
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
                onChange={(e) => setFormData((prev) => ({ ...prev, active: e.target.checked }))}
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
                  {translateRoleName(role.name)}
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
