import React from 'react';
import { useTranslation } from 'react-i18next';
import './ui.css';

export interface Column<T> {
  key: string;
  header: React.ReactNode;
  render: (row: T) => React.ReactNode;
  width?: string;
  align?: 'left' | 'center' | 'right';
}

interface DataTableProps<T> {
  columns: Column<T>[];
  data: T[];
  rowKey: (row: T) => string | number;
  onRowClick?: (row: T) => void;
  emptyMessage?: string;
  loading?: boolean;
  skeletonRows?: number;
  striped?: boolean;
  className?: string;
}

/**
 * One table implementation for the whole app. Replaces the per-page
 * hand-rolled <table> variants which drifted in alignment/padding/loading.
 */
export function DataTable<T>({
  columns,
  data,
  rowKey,
  onRowClick,
  emptyMessage,
  loading = false,
  skeletonRows = 5,
  striped = true,
  className = ''
}: DataTableProps<T>) {
  const { t } = useTranslation();

  return (
    <div className={`data-table-wrapper ${className}`}>
      <table className="data-table">
        <thead>
          <tr>
            {columns.map((column) => (
              <th
                key={column.key}
                style={column.width ? { width: column.width } : undefined}
                className={column.align ? `align-${column.align}` : undefined}
              >
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {loading ? (
            Array.from({ length: skeletonRows }).map((_, index) => (
              <tr key={`skel-${index}`} className="skeleton-row">
                {columns.map((column) => (
                  <td key={column.key}>
                    <div className="skeleton-line" />
                  </td>
                ))}
              </tr>
            ))
          ) : data.length === 0 ? (
            <tr>
              <td colSpan={columns.length} className="empty-row">
                {emptyMessage ?? t('common.noData')}
              </td>
            </tr>
          ) : (
            data.map((row) => (
              <tr
                key={rowKey(row)}
                className={`${onRowClick ? 'clickable-row' : ''} ${striped ? 'striped' : ''}`}
                onClick={onRowClick ? () => onRowClick(row) : undefined}
                tabIndex={onRowClick ? 0 : undefined}
                onKeyDown={
                  onRowClick
                    ? (e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault();
                          onRowClick(row);
                        }
                      }
                    : undefined
                }
              >
                {columns.map((column) => (
                  <td
                    key={column.key}
                    className={column.align ? `align-${column.align}` : undefined}
                  >
                    {column.render(row)}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
