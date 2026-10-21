import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { EmployeePermission, EmployeeListItem, Permission } from '../types';
import { apiClient } from '../utils/api';
import BaseModal from './BaseModal';
import './PermissionsModal.css';

interface Props {
  employee: EmployeeListItem;
  getPermissionLabel: (permissionCode: string, fallback?: string) => string;
  onClose: () => void;
  onSaved: () => void;
}

const PermissionsModal: React.FC<Props> = ({ employee, getPermissionLabel, onClose, onSaved }) => {
  const { t } = useTranslation();
  const [permissions, setPermissions] = useState<Permission[]>([]);
  const [employeePerms, setEmployeePerms] = useState<EmployeePermission[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedPermId, setSelectedPermId] = useState<number | null>(null);
  const [effect, setEffect] = useState<'Allow' | 'Deny'>('Allow');
  const [scopeType, setScopeType] = useState('');
  const [scopeId, setScopeId] = useState('');

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [employee.employeeId]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [perms, empPerms] = await Promise.all([
        apiClient.get<Permission[]>('/api/admin/access/permissions'),
        apiClient.get<EmployeePermission[]>(`/api/admin/access/employees/${employee.employeeId}/rules`)
      ]);
      setPermissions(perms);
      setEmployeePerms(empPerms);
      if (perms.length && !perms.some((p) => p.id === selectedPermId)) {
        setSelectedPermId(perms[0].id);
      }
    } catch (err) {
      console.error('Error loading permissions', err);
      setError(t('admin.accessRules.loadFailed'));
    } finally {
      setLoading(false);
    }
  };

  const handleAdd = async () => {
    if (!selectedPermId) return;
    setSaving(true);
    setError(null);
    try {
      const scopeIdValue = parseInt(scopeId, 10);
      await apiClient.post(`/api/admin/access/employees/${employee.employeeId}/permissions`, {
        permissionId: selectedPermId,
        effect,
        scopeType: scopeType || null,
        scopeId: Number.isNaN(scopeIdValue) ? null : scopeIdValue
      });
      await load();
      onSaved();
    } catch (err) {
      console.error('Error adding employee permission', err);
      setError(t('admin.accessRules.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id: number) => {
    setSaving(true);
    setError(null);
    try {
      await apiClient.delete(`/api/admin/access/employee-permissions/${id}`);
      await load();
      onSaved();
    } catch (err) {
      console.error('Error deleting employee permission', err);
      setError(t('admin.accessRules.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  const codeFor = (permissionId: number) =>
    permissions.find((p) => p.id === permissionId)?.code;

  return (
    <BaseModal
      title={t('admin.accessRules.title', { name: employee.fullName })}
      onClose={onClose}
      contentClassName="permissions-modal"
    >
        {loading ? (
          <div className="employee-form">{t('common.loading')}</div>
        ) : (
          <div className="modal-body">
            {error && <div className="alert alert-error">{error}</div>}
            <p className="access-rules-help">{t('admin.accessRules.employeeHelp')}</p>

            <div className="perm-add">
              <select
                value={selectedPermId ?? ''}
                onChange={(e) => setSelectedPermId(parseInt(e.target.value, 10) || null)}
              >
                {permissions.map((p) => (
                  <option key={p.id} value={p.id}>{getPermissionLabel(p.code, p.code)}</option>
                ))}
              </select>
              <select value={effect} onChange={(e) => setEffect(e.target.value as 'Allow' | 'Deny')}>
                <option value="Allow">{t('admin.accessRules.allow')}</option>
                <option value="Deny">{t('admin.accessRules.deny')}</option>
              </select>
              <input
                placeholder={t('admin.accessRules.scopeTypePlaceholder')}
                value={scopeType}
                onChange={(e) => setScopeType(e.target.value)}
              />
              <input
                placeholder={t('admin.accessRules.scopeIdPlaceholder')}
                value={scopeId}
                onChange={(e) => setScopeId(e.target.value)}
              />
              <button className="btn btn-primary" onClick={handleAdd} disabled={saving || !selectedPermId}>
                {t('admin.accessRules.addPermission')}
              </button>
            </div>

            <div className="perm-list">
              <h4>{t('admin.accessRules.employeeOverrides')}</h4>
              {employeePerms.length === 0 ? (
                <p className="no-data">{t('admin.accessRules.noEmployeeOverrides')}</p>
              ) : (
                <table>
                  <thead>
                    <tr>
                      <th>{t('admin.accessRules.permissionColumn')}</th>
                      <th>{t('admin.accessRules.effectColumn')}</th>
                      <th>{t('admin.accessRules.scope')}</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {employeePerms.map((ep) => (
                      <tr key={ep.id}>
                        <td>{getPermissionLabel(codeFor(ep.permissionId) || String(ep.permissionId))}</td>
                        <td>{ep.effect === 'Deny' ? t('admin.accessRules.deny') : t('admin.accessRules.allow')}</td>
                        <td>
                          {ep.scopeType
                            ? `${ep.scopeType}${ep.scopeId ? ` #${ep.scopeId}` : ''}`
                            : t('admin.accessRules.globalScope')}
                        </td>
                        <td>
                          <button
                            className="btn btn-secondary btn-sm"
                            onClick={() => handleDelete(ep.id)}
                            disabled={saving}
                          >
                            {t('common.delete')}
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        )}
    </BaseModal>
  );
};

export default PermissionsModal;
