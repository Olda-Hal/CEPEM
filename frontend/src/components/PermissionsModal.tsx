import React, { useEffect, useState } from 'react';
import { Permission, EmployeePermission } from '../types';
import { apiClient } from '../utils/api';
import './PermissionsModal.css';

interface Props {
  employeeId: number;
  onClose: () => void;
}

export const PermissionsModal: React.FC<Props> = ({ employeeId, onClose }) => {
  const [permissions, setPermissions] = useState<Permission[]>([]);
  const [employeePerms, setEmployeePerms] = useState<EmployeePermission[]>([]);
  const [selectedPermId, setSelectedPermId] = useState<number | null>(null);
  const [effect, setEffect] = useState<'Allow'|'Deny'>('Allow');
  const [scopeType, setScopeType] = useState<string>('');
  const [scopeId, setScopeId] = useState<string>('');

  useEffect(() => { load(); }, []);

  const load = async () => {
    const [perms, empPerms] = await Promise.all([
      apiClient.get<Permission[]>('/api/admin/permissions'),
      apiClient.get<EmployeePermission[]>(`/api/admin/employee-permissions?employeeId=${employeeId}`)
    ]);
    setPermissions(perms);
    setEmployeePerms(empPerms);
    if (perms.length) setSelectedPermId(perms[0].id);
  };

  const handleAdd = async () => {
    if (!selectedPermId) return;
    const body = {
      employeeId: employeeId,
      permissionId: selectedPermId,
      effect,
      scopeType: scopeType || null,
      scopeId: scopeId ? parseInt(scopeId, 10) : null
    };
    await apiClient.post('/api/admin/employee-permissions', body);
    await load();
  };

  const handleDelete = async (id: number) => {
    await apiClient.delete(`/api/admin/employee-permissions/${id}`);
    await load();
  };

  return (
    <div className="modal-overlay">
      <div className="modal-content permissions-modal">
        <div className="modal-header">
          <h3>Permissions for employee #{employeeId}</h3>
          <button className="close-button" onClick={onClose}>&times;</button>
        </div>

        <div className="modal-body">
          <div className="perm-add">
            <select value={selectedPermId ?? ''} onChange={e => setSelectedPermId(parseInt(e.target.value, 10))}>
              {permissions.map(p => (
                <option key={p.id} value={p.id}>{p.code}</option>
              ))}
            </select>
            <select value={effect} onChange={e => setEffect(e.target.value as 'Allow'|'Deny')}>
              <option value="Allow">Allow</option>
              <option value="Deny">Deny</option>
            </select>
            <input placeholder="Scope type (e.g. Hospital)" value={scopeType} onChange={e=>setScopeType(e.target.value)} />
            <input placeholder="Scope id" value={scopeId} onChange={e=>setScopeId(e.target.value)} />
            <button className="btn" onClick={handleAdd}>Add</button>
          </div>

          <div className="perm-list">
            <h4>Employee overrides</h4>
            <table>
              <thead><tr><th>Permission</th><th>Effect</th><th>Scope</th><th/></tr></thead>
              <tbody>
                {employeePerms.map(ep => (
                  <tr key={ep.id}>
                    <td>{permissions.find(p => p.id === ep.permissionId)?.code || ep.permissionId}</td>
                    <td>{ep.effect}</td>
                    <td>{ep.scopeType || 'Global'} {ep.scopeId ? `#${ep.scopeId}` : ''}</td>
                    <td><button onClick={() => handleDelete(ep.id)}>Delete</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
};

export default PermissionsModal;
