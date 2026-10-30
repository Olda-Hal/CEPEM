import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { apiClient } from '../utils/api';
import BaseModal from '../components/BaseModal';
import { CreateRoleRequest, Permission, Role, RolePermissionAssignment } from '../types';
import { APP_LANDING_PATHS } from '../utils/landing';
import './RoleManagementPage.css';

const RoleManagementPage: React.FC = () => {
  const { t } = useTranslation();
  const [roles, setRoles] = useState<Role[]>([]);
  const [catalog, setCatalog] = useState<Permission[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [selectedRole, setSelectedRole] = useState<Role | null>(null);
  const [showRulesModal, setShowRulesModal] = useState(false);
  const [newRoleName, setNewRoleName] = useState('');
  const [creatingRole, setCreatingRole] = useState(false);
  const [confirmDeleteRole, setConfirmDeleteRole] = useState<Role | null>(null);
  const [deletingRole, setDeletingRole] = useState(false);

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const [rolesResponse, catalogResponse] = await Promise.all([
          apiClient.get<Role[]>('/api/admin/roles'),
          apiClient.get<Permission[]>('/api/admin/access/permissions')
        ]);
        setRoles(rolesResponse);
        setCatalog(catalogResponse);
      } catch (err) {
        console.error('Error loading role management data', err);
        setError(t('admin.roleEditor.loadFailed'));
      } finally {
        setLoading(false);
      }
    };

    void load();
  }, [t]);

  const permissionCount = useMemo(() => catalog.length, [catalog.length]);

  const handleCreateRole = async (e: React.FormEvent) => {
    e.preventDefault();
    const roleName = newRoleName.trim();
    if (!roleName) {
      setError(t('admin.roleEditor.roleNameRequired'));
      return;
    }

    setCreatingRole(true);
    setError(null);
    setSuccess(null);
    try {
      const request: CreateRoleRequest = { name: roleName };
      const created = await apiClient.post<Role>('/api/admin/roles', request);
      setRoles((prev) => [...prev, created].sort((a, b) => a.name.localeCompare(b.name)));
      setNewRoleName('');
      setSuccess(t('admin.roleEditor.created', { name: created.name }));
    } catch (err) {
      console.error('Error creating role', err);
      setError(t('admin.roleEditor.createFailed'));
    } finally {
      setCreatingRole(false);
    }
  };

  const handleDeleteRole = async () => {
    if (!confirmDeleteRole) {
      return;
    }

    setDeletingRole(true);
    setError(null);
    setSuccess(null);
    try {
      await apiClient.delete(`/api/admin/roles/${confirmDeleteRole.id}`);
      setRoles((prev) => prev.filter((r) => r.id !== confirmDeleteRole.id));
      setSuccess(
        t('admin.roleEditor.deleted', { name: translateRoleName(confirmDeleteRole.name) })
      );
      setConfirmDeleteRole(null);
    } catch (err) {
      console.error('Error deleting role', err);
      setError(t('admin.roleEditor.deleteFailed'));
    } finally {
      setDeletingRole(false);
    }
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

  const openRules = (role: Role) => {
    setSelectedRole(role);
    setShowRulesModal(true);
  };

  const handleDefaultPathChange = async (role: Role, defaultPath: string) => {
    setError(null);
    setSuccess(null);
    try {
      await apiClient.put(`/api/admin/roles/${role.id}/default-path`, {
        defaultPath: defaultPath || null
      });
      setRoles((prev) =>
        prev.map((r) => (r.id === role.id ? { ...r, defaultPath: defaultPath || null } : r))
      );
    } catch (err) {
      console.error('Error updating role default page', err);
      setError(t('admin.roleEditor.defaultPathFailed'));
    }
  };

  if (loading) {
    return (
      <div className="role-management-page">
        <h1 className="page-title">{t('admin.roleEditor.title')}</h1>
        <div className="role-management-content">{t('common.loading')}</div>
      </div>
    );
  }

  return (
    <div className="role-management-page">
      <h1 className="page-title">{t('admin.roleEditor.title')}</h1>

      <div className="role-management-content">
        {error && <div className="alert alert-error">{error}</div>}
        {success && <div className="alert alert-success">{success}</div>}

        <div className="role-top-grid">
          <form className="role-create-card" onSubmit={handleCreateRole}>
            <h3>{t('admin.roleEditor.createTitle')}</h3>
            <p>{t('admin.roleEditor.createDescription')}</p>
            <div className="role-create-row">
              <input
                type="text"
                placeholder={t('admin.roleEditor.roleNamePlaceholder')}
                value={newRoleName}
                onChange={(e) => setNewRoleName(e.target.value)}
              />
              <button className="btn btn-primary" type="submit" disabled={creatingRole}>
                {creatingRole
                  ? t('admin.roleEditor.creatingButton')
                  : t('admin.roleEditor.createButton')}
              </button>
            </div>
          </form>

          <div className="role-meta-card">
            <h3>{t('admin.roleEditor.catalogTitle')}</h3>
            <p>{t('admin.roleEditor.catalogDescription')}</p>
            <div className="meta-number">{permissionCount}</div>
          </div>
        </div>

        <div className="roles-table-card">
          <table className="roles-table">
            <thead>
              <tr>
                <th>{t('admin.roleEditor.rolesColumn')}</th>
                <th>{t('admin.roleEditor.defaultPageColumn')}</th>
                <th>{t('admin.roleEditor.actionsColumn')}</th>
              </tr>
            </thead>
            <tbody>
              {roles.map((role) => (
                <tr key={role.id}>
                  <td>{translateRoleName(role.name)}</td>
                  <td>
                    <select
                      value={role.defaultPath ?? ''}
                      onChange={(e) => void handleDefaultPathChange(role, e.target.value)}
                    >
                      <option value="">{t('admin.roleEditor.defaultPageNone')}</option>
                      {APP_LANDING_PATHS.map((path) => (
                        <option key={path} value={path}>
                          {path}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td>
                    <button className="btn btn-secondary btn-sm" onClick={() => openRules(role)}>
                      {t('admin.roleEditor.configureButton')}
                    </button>
                    {role.name !== 'SysAdmin' && (
                      <button
                        className="btn btn-danger btn-sm"
                        title={t('admin.roleEditor.deleteButton')}
                        aria-label={t('admin.roleEditor.deleteButton')}
                        onClick={() => setConfirmDeleteRole(role)}
                      >
                        {t('admin.roleEditor.deleteButton')}
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {confirmDeleteRole && (
        <BaseModal
          title={t('admin.roleEditor.deleteTitle')}
          onClose={() => setConfirmDeleteRole(null)}
        >
          <p>
            {t('admin.roleEditor.deleteConfirm', {
              name: translateRoleName(confirmDeleteRole.name)
            })}
          </p>
          <div className="modal-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => setConfirmDeleteRole(null)}
              disabled={deletingRole}
            >
              {t('common.cancel')}
            </button>
            <button
              type="button"
              className="btn btn-danger"
              onClick={handleDeleteRole}
              disabled={deletingRole}
            >
              {deletingRole ? t('admin.roleEditor.deleting') : t('admin.roleEditor.deleteButton')}
            </button>
          </div>
        </BaseModal>
      )}

      {showRulesModal && selectedRole && (
        <RoleRulesModal
          role={selectedRole}
          catalog={catalog}
          onClose={() => {
            setShowRulesModal(false);
            setSelectedRole(null);
          }}
          onSaved={() =>
            setSuccess(
              t('admin.roleEditor.rulesSaved', { name: translateRoleName(selectedRole.name) })
            )
          }
          translateRoleName={translateRoleName}
          getPermissionLabel={getPermissionLabel}
        />
      )}
    </div>
  );
};

interface RoleRulesModalProps {
  role: Role;
  catalog: Permission[];
  onClose: () => void;
  onSaved: () => void;
  translateRoleName: (roleName: string) => string;
  getPermissionLabel: (permissionKey: string, fallback?: string) => string;
}

const RoleRulesModal: React.FC<RoleRulesModalProps> = ({
  role,
  catalog,
  onClose,
  onSaved,
  translateRoleName,
  getPermissionLabel
}) => {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Assignment per permission code: 'Allow' | 'Deny' | undefined (unset)
  const [assignments, setAssignments] = useState<Record<string, 'Allow' | 'Deny' | undefined>>({});

  useEffect(() => {
    const loadRules = async () => {
      setLoading(true);
      setError(null);
      try {
        const response = await apiClient.get<RolePermissionAssignment[]>(
          `/api/admin/access/roles/${role.id}/rules`
        );
        const initial: Record<string, 'Allow' | 'Deny' | undefined> = {};
        response.forEach((assignment) => {
          initial[assignment.code] = assignment.effect === 'Deny' ? 'Deny' : 'Allow';
        });
        setAssignments(initial);
      } catch (err) {
        console.error('Error loading role permissions', err);
        setError(t('admin.accessRules.loadFailed'));
      } finally {
        setLoading(false);
      }
    };

    void loadRules();
  }, [role.id, t]);

  const updateAssignment = (code: string, effect: 'Allow' | 'Deny' | undefined) => {
    setAssignments((prev) => ({ ...prev, [code]: effect }));
  };

  const handleSave = async () => {
    setSaving(true);
    setError(null);

    try {
      const permissions = Object.entries(assignments)
        .filter(([, effect]) => effect === 'Allow' || effect === 'Deny')
        .map(([code, effect]) => {
          const permission = catalog.find((p) => p.code === code);
          return { permissionId: permission?.id ?? 0, effect };
        });

      await apiClient.put(`/api/admin/access/roles/${role.id}/rules`, { permissions });
      onSaved();
      onClose();
    } catch (err) {
      console.error('Error saving role permissions', err);
      setError(t('admin.accessRules.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <BaseModal
      title={t('admin.accessRules.roleTitle', { name: translateRoleName(role.name) })}
      onClose={onClose}
      contentClassName="role-access-modal-content"
    >
      {loading ? (
        <div className="employee-form">{t('common.loading')}</div>
      ) : (
        <div className="employee-form">
          {error && <div className="alert alert-error">{error}</div>}
          <p className="access-rules-help">{t('admin.accessRules.roleHelp')}</p>
          <div className="access-rules-grid">
            {catalog.map((permission) => {
              const effect = assignments[permission.code];
              const displayName = getPermissionLabel(permission.code, permission.code);

              return (
                <div className="access-rule-row" key={permission.code}>
                  <div className="access-rule-key">
                    <div className="permission-title">
                      {displayName}
                      {permission.description && (
                        <span
                          className="permission-info"
                          title={permission.description}
                          aria-label={permission.description}
                        >
                          i
                        </span>
                      )}
                    </div>
                  </div>
                  <select
                    value={effect || ''}
                    onChange={(e) =>
                      updateAssignment(
                        permission.code,
                        (e.target.value || undefined) as 'Allow' | 'Deny' | undefined
                      )
                    }
                  >
                    <option value="">{t('admin.accessRules.none')}</option>
                    <option value="Allow">{t('admin.accessRules.allow')}</option>
                    <option value="Deny">{t('admin.accessRules.deny')}</option>
                  </select>
                </div>
              );
            })}
          </div>

          <div className="modal-actions">
            <button type="button" className="btn btn-secondary" onClick={onClose} disabled={saving}>
              {t('admin.accessRules.close')}
            </button>
            <button
              type="button"
              className="btn btn-primary"
              onClick={handleSave}
              disabled={saving}
            >
              {saving ? t('admin.accessRules.savingRules') : t('admin.accessRules.saveRules')}
            </button>
          </div>
        </div>
      )}
    </BaseModal>
  );
};

export default RoleManagementPage;
