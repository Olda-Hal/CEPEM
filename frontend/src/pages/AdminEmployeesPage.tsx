import React, { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import './AdminEmployeesPage.css';
import {
  AdminScope,
  EmployeeListItem,
  Hospital,
  Role,
  UpdateEmployeeRequest,
  UpdateEmployeeResponse
} from '../types';
import { apiClient, getAuthToken } from '../utils/api';
import PermissionsModal from '../components/PermissionsModal';
import { CreateEmployeeModal } from '../components/CreateEmployeeModal';
import { DataTable } from '../components/ui/DataTable';
import { useAuth } from '../contexts/AuthContext';
import {
  countryScopeByCode,
  countryCodeByScopeId,
  countryOptions,
  normalizeCountryCode
} from '../utils/countryScope';

const EMPLOYEES_PER_PAGE = 25;

const AdminEmployeesPage: React.FC = () => {
  const { t } = useTranslation();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [selectedEmployee, setSelectedEmployee] = useState<EmployeeListItem | null>(null);
  const [showEditModal, setShowEditModal] = useState(false);
  const [showCreateModal, setShowCreateModal] = useState(false);
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
      const [employees, roles, scope, hospitals] = await Promise.all([
        apiClient.get<EmployeeListItem[]>('/api/admin/employees'),
        apiClient.get<Role[]>('/api/admin/roles'),
        apiClient.get<AdminScope>('/api/admin/employees/scope'),
        apiClient.get<Hospital[]>('/api/hospitals')
      ]);
      return { employees, roles, scope, hospitals };
    }
  });

  const loadData = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: ['admin', 'employees-and-roles'] });
  }, [queryClient]);

  const scope = data?.scope ?? null;
  const isScopedAdmin =
    !!scope && !scope.hasFullAccess && (scope.isCountryAdmin || scope.isCenterAdmin);

  // Mirrors the server-side EmployeeScopeService visibility rule so action
  // buttons are only offered for employees the actor may actually manage.
  const canManageEmployee = (employee: EmployeeListItem) => {
    if (!isScopedAdmin || !scope) {
      return true;
    }

    if (scope.isCenterAdmin && employee.hospitalIds?.some((id) => scope.hospitalIds.includes(id))) {
      return true;
    }

    if (scope.isCountryAdmin) {
      const employeeScopeId = countryScopeByCode[normalizeCountryCode(employee.countryCode)] ?? -1;
      if (scope.administeredCountryScopeIds.includes(employeeScopeId)) {
        return true;
      }
      if (employee.createdByEmployeeId === user?.id) {
        return true;
      }
    }

    return false;
  };

  // Role checkboxes the actor may grant (full access → everything)
  const assignableRoles = (data?.roles ?? []).filter(
    (role) => !isScopedAdmin || !scope || scope.assignableRoleNames.includes(role.name)
  );

  // Country options the actor may assign
  const allowedCountryCodes =
    !isScopedAdmin || !scope
      ? countryOptions
      : scope.administeredCountryScopeIds
          .map((scopeId) => countryCodeByScopeId[scopeId])
          .filter((code): code is string => Boolean(code));

  // Center assignment options the actor may grant
  const allowedHospitals = (data?.hospitals ?? []).filter((hospital) => {
    if (!isScopedAdmin || !scope) {
      return true;
    }
    if (scope.isCenterAdmin && !scope.isCountryAdmin) {
      return scope.hospitalIds.includes(hospital.id);
    }
    return scope.administeredCountryScopeIds.includes(hospital.countryScopeId ?? -1);
  });

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

  const totalPages = Math.max(1, Math.ceil(employees.length / EMPLOYEES_PER_PAGE));
  const pagedEmployees = employees.slice(
    page * EMPLOYEES_PER_PAGE,
    (page + 1) * EMPLOYEES_PER_PAGE
  );

  return (
    <div className="admin-employees-page">
      <div className="page-heading">
        <h1 className="page-title">{t('admin.employeeManagement')}</h1>
        <div className="page-heading-actions">
          <button className="btn btn-secondary" onClick={handleExport}>
            {t('activityLog.exportCsv')}
          </button>
          <button className="btn btn-primary" onClick={() => setShowCreateModal(true)}>
            + {t('createEmployee.buttonText')}
          </button>
        </div>
      </div>

      {error && <div className="alert alert-error">{error}</div>}
      {success && <div className="alert alert-success">{success}</div>}

      <div className="employees-table-container">
        <DataTable
          columns={[
            {
              key: 'name',
              header: t('admin.fullName'),
              render: (employee) => (
                <div className="employee-name">
                  <strong>{employee.fullName}</strong>
                  <small>{employee.gender}</small>
                </div>
              )
            },
            { key: 'email', header: t('admin.email'), render: (employee) => employee.email },
            { key: 'uid', header: t('admin.uid'), render: (employee) => employee.uid },
            { key: 'phone', header: t('admin.phone'), render: (employee) => employee.phoneNumber },
            {
              key: 'roles',
              header: t('admin.roles'),
              render: (employee) => (
                <div className="roles">
                  {employee.roles.map((role, index) => (
                    <span key={index} className="role-badge">
                      {translateRoleName(role)}
                    </span>
                  ))}
                </div>
              )
            },
            {
              key: 'status',
              header: t('admin.status'),
              render: (employee) => (
                <span
                  className={`status-badge status-${employee.active ? 'active' : 'unavailable'}`}
                >
                  {employee.active ? t('admin.active') : t('admin.inactive')}
                </span>
              )
            },
            {
              key: 'lastLogin',
              header: t('admin.lastLogin'),
              render: (employee) =>
                employee.lastLoginAt
                  ? new Date(employee.lastLoginAt).toLocaleString()
                  : t('admin.never')
            },
            {
              key: 'actions',
              header: t('admin.actions'),
              render: (employee) => (
                <div className="action-buttons">
                  <button
                    className="btn btn-primary btn-sm"
                    onClick={() => handleEdit(employee)}
                    disabled={!canManageEmployee(employee)}
                  >
                    {t('admin.edit')}
                  </button>
                  {!isScopedAdmin && (
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
              )
            }
          ]}
          data={pagedEmployees}
          rowKey={(employee) => employee.employeeId}
        />
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

      {showCreateModal && (
        <CreateEmployeeModal
          isOpen
          onClose={() => setShowCreateModal(false)}
          onEmployeeCreated={() => {
            setShowCreateModal(false);
            void loadData();
          }}
          roles={assignableRoles}
          hospitals={allowedHospitals}
          allowedCountryCodes={allowedCountryCodes}
          defaultCountryCode={normalizeCountryCode(user?.countryCode)}
          defaultHospitalIds={
            scope && !scope.hasFullAccess && scope.isCenterAdmin && !scope.isCountryAdmin
              ? scope.hospitalIds
              : []
          }
        />
      )}

      {showEditModal && selectedEmployee && (
        <EmployeeEditModal
          employee={selectedEmployee}
          roles={assignableRoles}
          hospitals={allowedHospitals}
          allowedCountryCodes={allowedCountryCodes}
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
  hospitals: Hospital[];
  allowedCountryCodes: string[];
  onSave: (employee: UpdateEmployeeRequest) => void;
  onCancel: () => void;
}

const EmployeeEditModal: React.FC<EmployeeEditModalProps> = ({
  employee,
  roles,
  hospitals,
  allowedCountryCodes,
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
    roleIds: [],
    countryCode: normalizeCountryCode(employee.countryCode),
    hospitalIds: employee.hospitalIds ?? []
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

  const handleHospitalChange = (hospitalId: number, checked: boolean) => {
    setFormData((prev) => ({
      ...prev,
      hospitalIds: checked
        ? [...(prev.hospitalIds ?? []), hospitalId]
        : (prev.hospitalIds ?? []).filter((id) => id !== hospitalId)
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

          <div className="form-row">
            <div className="form-group">
              <label>{t('admin.country')}</label>
              <select
                value={formData.countryCode}
                onChange={(e) => setFormData((prev) => ({ ...prev, countryCode: e.target.value }))}
              >
                {allowedCountryCodes.map((code) => (
                  <option key={code} value={code}>
                    {code}
                  </option>
                ))}
              </select>
            </div>
            <div className="form-group">
              <label>{t('admin.centersLabel')}</label>
              <div className="roles-checkboxes">
                {hospitals.length === 0 && <small>{t('admin.noCentersAvailable')}</small>}
                {hospitals.map((hospital) => (
                  <label key={hospital.id} className="checkbox-label">
                    <input
                      type="checkbox"
                      checked={(formData.hospitalIds ?? []).includes(hospital.id)}
                      onChange={(e) => handleHospitalChange(hospital.id, e.target.checked)}
                    />
                    {hospital.name ?? `#${hospital.id}`}
                  </label>
                ))}
              </div>
            </div>
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
