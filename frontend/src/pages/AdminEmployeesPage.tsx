import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import './AdminEmployeesPage.css';
import {
  EmployeeListItem,
  Role,
  UpdateEmployeeRequest,
  UpdateEmployeeResponse,
  EndpointPermissionCatalogItem,
  EmployeePermissionRulesResponse,
  PermissionRule,
  UpdateEmployeePermissionRulesRequest
} from '../types';
import { apiClient } from '../utils/api';
import { AppHeader } from '../components/AppHeader';
import { useAuth } from '../contexts/AuthContext';
import { hasRole } from '../utils/roles';

const AdminEmployeesPage: React.FC = () => {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [employees, setEmployees] = useState<EmployeeListItem[]>([]);
  const [roles, setRoles] = useState<Role[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedEmployee, setSelectedEmployee] = useState<EmployeeListItem | null>(null);
  const [showEditModal, setShowEditModal] = useState(false);
  const [showRulesModal, setShowRulesModal] = useState(false);
  const [selectedRulesEmployee, setSelectedRulesEmployee] = useState<EmployeeListItem | null>(null);
  const [permissionCatalog, setPermissionCatalog] = useState<EndpointPermissionCatalogItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const isRestrictedCountryAdmin = hasRole(user, 'Country Admin') && !hasRole(user, 'SysAdmin') && !hasRole(user, 'Admin');

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
    const translationKey = `admin.permissionLabels.${permissionKey.toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_|_$/g, '')}`;
    return t(translationKey, { defaultValue: fallback || permissionKey });
  };

  const getResourceTypeLabel = (resourceType?: string) => {
    if (!resourceType) {
      return '';
    }

    return t(`admin.accessRules.resourceTypes.${resourceType.toLowerCase()}`, { defaultValue: resourceType });
  };

  const loadData = useCallback(async () => {
    try {
      const [employeesResponse, rolesResponse] = await Promise.all([
        apiClient.get<EmployeeListItem[]>('/api/admin/employees'),
        apiClient.get<Role[]>('/api/admin/roles')
      ]);
      setEmployees(employeesResponse);
      setRoles(rolesResponse);

      try {
        const catalog = await apiClient.get<EndpointPermissionCatalogItem[]>('/api/admin/access/permissions');
        setPermissionCatalog(catalog);
      } catch {
        setPermissionCatalog([]);
      }
    } catch (error) {
      console.error('Error loading data:', error);
      setError(t('admin.dataLoadFailed'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

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
                      <span key={index} className="role-badge">{translateRoleName(role)}</span>
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
        <AccessRulesModal
          employee={selectedRulesEmployee}
          catalog={permissionCatalog}
          onClose={() => {
            setShowRulesModal(false);
            setSelectedRulesEmployee(null);
          }}
          onSaved={() => {
            setSuccess(t('admin.accessSaved'));
          }}
          translateRoleName={translateRoleName}
          getPermissionLabel={getPermissionLabel}
          getResourceTypeLabel={getResourceTypeLabel}
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

interface AccessRulesModalProps {
  employee: EmployeeListItem;
  catalog: EndpointPermissionCatalogItem[];
  onClose: () => void;
  onSaved: () => void;
  translateRoleName: (roleName: string) => string;
  getPermissionLabel: (permissionKey: string, fallback?: string) => string;
  getResourceTypeLabel: (resourceType?: string) => string;
}

interface EditableRuleState {
  effect: '' | 'allow' | 'deny';
  scopesText: string;
  resourceType?: string;
  hasPlaceholderScope?: boolean;
}

const AccessRulesModal: React.FC<AccessRulesModalProps> = ({ employee, catalog, onClose, onSaved, getPermissionLabel, getResourceTypeLabel }) => {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [rules, setRules] = useState<Record<string, EditableRuleState>>({});

  const catalogByKey = useMemo(() => {
    return new Map(catalog.map((item) => [item.permissionKey, item]));
  }, [catalog]);

  const renderedPermissionKeys = useMemo(() => {
    const keys = catalog.map((item) => item.permissionKey);
    Object.keys(rules).forEach((permissionKey) => {
      if (!catalogByKey.has(permissionKey)) {
        keys.push(permissionKey);
      }
    });

    return keys;
  }, [catalog, rules, catalogByKey]);

  useEffect(() => {
    const loadRules = async () => {
      setLoading(true);
      try {
        const response = await apiClient.get<EmployeePermissionRulesResponse>(`/api/admin/access/employees/${employee.employeeId}/rules`);

        const initialRules: Record<string, EditableRuleState> = {};
        catalog.forEach((item) => {
          initialRules[item.permissionKey] = {
            effect: '',
            scopesText: '',
            resourceType: item.resourceType
          };
        });

        response.rules.forEach((rule) => {
          const positiveScopes = rule.scopes.filter((s) => s.resourceId > 0);
          const hasPlaceholderScope = rule.scopes.some((s) => s.resourceId === 0);
          const scopesText = positiveScopes.map((s) => s.resourceId).join(', ');
          if (!initialRules[rule.permissionKey]) {
            initialRules[rule.permissionKey] = {
              effect: rule.effect,
              scopesText,
              resourceType: rule.scopes[0]?.resourceType,
              hasPlaceholderScope
            };
          } else {
            initialRules[rule.permissionKey].effect = rule.effect;
            initialRules[rule.permissionKey].scopesText = scopesText;
            initialRules[rule.permissionKey].resourceType = initialRules[rule.permissionKey].resourceType || rule.scopes[0]?.resourceType;
            initialRules[rule.permissionKey].hasPlaceholderScope = hasPlaceholderScope;
          }
        });

        setRules(initialRules);
      } catch (err) {
        console.error('Error loading access rules', err);
        setError(t('admin.accessRules.loadFailed'));
      } finally {
        setLoading(false);
      }
    };

    void loadRules();
  }, [catalog, employee.employeeId, t]);

  const updateRule = (permissionKey: string, patch: Partial<EditableRuleState>) => {
    setRules((prev) => ({
      ...prev,
      [permissionKey]: {
        ...(prev[permissionKey] || { effect: '', scopesText: '' }),
        ...patch
      }
    }));
  };

  const parseScopes = (scopesText: string, resourceType?: string, hasPlaceholderScope?: boolean) => {
    if (!resourceType || !scopesText.trim()) {
      return hasPlaceholderScope ? [{ resourceType: resourceType || 'Hospital', resourceId: 0 }] : [];
    }

    return scopesText
      .split(',')
      .map((item) => Number(item.trim()))
      .filter((value) => Number.isInteger(value) && value > 0)
      .map((resourceId) => ({ resourceType, resourceId }));
  };

  const handleSave = async () => {
    setSaving(true);
    setError(null);

    try {
      const compiledRules: PermissionRule[] = Object.entries(rules)
        .filter(([, value]) => value.effect === 'allow' || value.effect === 'deny')
        .map(([permissionKey, value]) => ({
          permissionKey,
          effect: value.effect as 'allow' | 'deny',
          scopes: parseScopes(value.scopesText, value.resourceType, value.hasPlaceholderScope)
        }));

      const request: UpdateEmployeePermissionRulesRequest = { rules: compiledRules };
      await apiClient.put(`/api/admin/access/employees/${employee.employeeId}/rules`, request);
      onSaved();
      onClose();
    } catch (err) {
      console.error('Error saving access rules', err);
      setError(t('admin.accessRules.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="modal-overlay">
      <div className="modal-content access-modal-content">
        <div className="modal-header">
          <h2>{t('admin.accessRules.title', { name: employee.fullName })}</h2>
          <button className="close-button" onClick={onClose}>&times;</button>
        </div>

        {loading ? (
          <div className="employee-form">{t('common.loading')}</div>
        ) : (
          <div className="employee-form">
            {error && <div className="alert alert-error">{error}</div>}
            <p className="access-rules-help">{t('admin.accessRules.employeeHelp')}</p>
            <div className="access-rules-grid">
              {renderedPermissionKeys.map((permissionKey) => {
                const catalogItem = catalogByKey.get(permissionKey);
                const value = rules[permissionKey] || {
                  effect: '',
                  scopesText: '',
                  resourceType: catalogItem?.resourceType,
                  hasPlaceholderScope: false
                };
                const displayName = getPermissionLabel(permissionKey, catalogItem?.displayName || permissionKey);
                const endpoint = catalogItem?.endpoint || permissionKey;

                return (
                <div className="access-rule-row" key={permissionKey}>
                  <div className="access-rule-key">
                    <div className="permission-title">
                      {displayName}
                      <span className="permission-info" title={endpoint} aria-label={endpoint}>i</span>
                    </div>
                    {value.resourceType && (
                      <div className="permission-scope-hint">
                        {t('admin.accessRules.scope')}: {getResourceTypeLabel(value.resourceType)}
                        {catalogItem?.resourceHint ? ` (${catalogItem.resourceHint})` : ''}
                        {value.hasPlaceholderScope && !value.scopesText.trim() ? ` - ${t('admin.accessRules.assignIdsHint')}` : ''}
                      </div>
                    )}
                  </div>
                  <select
                    value={value.effect}
                    onChange={(e) => updateRule(permissionKey, { effect: e.target.value as '' | 'allow' | 'deny' })}
                  >
                    <option value="">{t('admin.accessRules.none')}</option>
                    <option value="allow">{t('admin.accessRules.allow')}</option>
                    <option value="deny">{t('admin.accessRules.deny')}</option>
                  </select>
                  <input
                    type="text"
                    placeholder={value.resourceType ? `${getResourceTypeLabel(value.resourceType)} ID: 1,2,3` : t('admin.accessRules.none')}
                    value={value.scopesText}
                    onChange={(e) => updateRule(permissionKey, { scopesText: e.target.value })}
                    disabled={!value.resourceType}
                  />
                </div>
                );
              })}
            </div>

            <div className="modal-actions">
              <button type="button" className="btn btn-secondary" onClick={onClose} disabled={saving}>
                {t('admin.accessRules.close')}
              </button>
              <button type="button" className="btn btn-primary" onClick={handleSave} disabled={saving}>
                {saving ? t('admin.accessRules.savingRules') : t('admin.accessRules.saveRules')}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default AdminEmployeesPage;
