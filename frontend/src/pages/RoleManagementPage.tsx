import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AppHeader } from '../components/AppHeader';
import { apiClient } from '../utils/api';
import {
  CreateRoleRequest,
  EndpointPermissionCatalogItem,
  PermissionRule,
  Role,
  RolePermissionRulesResponse,
  UpdateRolePermissionRulesRequest
} from '../types';
import './RoleManagementPage.css';

interface EditableRuleState {
  effect: '' | 'allow' | 'deny';
  scopesText: string;
  resourceType?: string;
  hasPlaceholderScope?: boolean;
}

const RoleManagementPage: React.FC = () => {
  const { t } = useTranslation();
  const [roles, setRoles] = useState<Role[]>([]);
  const [catalog, setCatalog] = useState<EndpointPermissionCatalogItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [selectedRole, setSelectedRole] = useState<Role | null>(null);
  const [showRulesModal, setShowRulesModal] = useState(false);
  const [newRoleName, setNewRoleName] = useState('');
  const [creatingRole, setCreatingRole] = useState(false);

  useEffect(() => {
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const [rolesResponse, catalogResponse] = await Promise.all([
          apiClient.get<Role[]>('/api/admin/roles'),
          apiClient.get<EndpointPermissionCatalogItem[]>('/api/admin/access/permissions')
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

  const openRules = (role: Role) => {
    setSelectedRole(role);
    setShowRulesModal(true);
  };

  if (loading) {
    return (
      <div className="role-management-page">
        <AppHeader sectionTitle={t('admin.roleEditor.title')} />
        <div className="role-management-content">{t('common.loading')}</div>
      </div>
    );
  }

  return (
    <div className="role-management-page">
      <AppHeader sectionTitle={t('admin.roleEditor.title')} />

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
                {creatingRole ? t('admin.roleEditor.creatingButton') : t('admin.roleEditor.createButton')}
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
                <th>{t('admin.roleEditor.actionsColumn')}</th>
              </tr>
            </thead>
            <tbody>
              {roles.map((role) => (
                <tr key={role.id}>
                  <td>{translateRoleName(role.name)}</td>
                  <td>
                    <button className="btn btn-secondary btn-sm" onClick={() => openRules(role)}>
                      {t('admin.roleEditor.configureButton')}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {showRulesModal && selectedRole && (
        <RoleRulesModal
          role={selectedRole}
          catalog={catalog}
          onClose={() => {
            setShowRulesModal(false);
            setSelectedRole(null);
          }}
          onSaved={() => setSuccess(t('admin.roleEditor.rulesSaved', { name: translateRoleName(selectedRole.name) }))}
          translateRoleName={translateRoleName}
          getPermissionLabel={getPermissionLabel}
          getResourceTypeLabel={getResourceTypeLabel}
        />
      )}
    </div>
  );
};

interface RoleRulesModalProps {
  role: Role;
  catalog: EndpointPermissionCatalogItem[];
  onClose: () => void;
  onSaved: () => void;
  translateRoleName: (roleName: string) => string;
  getPermissionLabel: (permissionKey: string, fallback?: string) => string;
  getResourceTypeLabel: (resourceType?: string) => string;
}

const RoleRulesModal: React.FC<RoleRulesModalProps> = ({ role, catalog, onClose, onSaved, translateRoleName, getPermissionLabel, getResourceTypeLabel }) => {
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
      setError(null);
      try {
        const response = await apiClient.get<RolePermissionRulesResponse>(`/api/admin/access/roles/${role.id}/rules`);

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
        console.error('Error loading role rules', err);
        setError(t('admin.accessRules.loadFailed'));
      } finally {
        setLoading(false);
      }
    };

    void loadRules();
  }, [catalog, role.id, t]);

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

      const request: UpdateRolePermissionRulesRequest = { rules: compiledRules };
      await apiClient.put(`/api/admin/access/roles/${role.id}/rules`, request);
      onSaved();
      onClose();
    } catch (err) {
      console.error('Error saving role rules', err);
      setError(t('admin.accessRules.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="modal-overlay">
      <div className="modal-content role-access-modal-content">
        <div className="modal-header">
          <h2>{t('admin.accessRules.roleTitle', { name: translateRoleName(role.name) })}</h2>
          <button className="close-button" onClick={onClose}>&times;</button>
        </div>

        {loading ? (
          <div className="employee-form">{t('common.loading')}</div>
        ) : (
          <div className="employee-form">
            {error && <div className="alert alert-error">{error}</div>}
            <p className="access-rules-help">{t('admin.accessRules.roleHelp')}</p>
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

export default RoleManagementPage;
