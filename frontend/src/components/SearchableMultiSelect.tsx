import React, { useState, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import './SearchableMultiSelect.css';

interface Option {
  id: number;
  name: string;
}

interface SearchableMultiSelectProps {
  options: Option[];
  selectedIds: number[];
  onChange: (selectedIds: number[]) => void;
  placeholder?: string;
  onAddNew?: (name: string) => Promise<Option>;
  label?: string;
  helpText?: string;
}

const SearchableMultiSelect: React.FC<SearchableMultiSelectProps> = ({
  options,
  selectedIds,
  onChange,
  placeholder,
  onAddNew,
  label,
  helpText
}) => {
  const { t } = useTranslation();
  const [searchQuery, setSearchQuery] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const filteredOptions = options.filter(option =>
    option.name.toLowerCase().includes(searchQuery.toLowerCase())
  ).sort((a, b) => a.name.localeCompare(b.name));

  const handleToggle = (id: number) => {
    if (selectedIds.includes(id)) {
      onChange(selectedIds.filter(selectedId => selectedId !== id));
    } else {
      onChange([...selectedIds, id]);
    }
  };

  const handleAddNew = async () => {
    if (onAddNew && searchQuery.trim()) {
      const newItem = await onAddNew(searchQuery.trim());
      if (newItem && newItem.id) {
        onChange([...selectedIds, newItem.id]);
      }
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      
      if (filteredOptions.length === 1) {
        handleToggle(filteredOptions[0].id);
      } else if (showAddButton) {
        handleAddNew();
      }
    }
  };  const selectedOptions = options.filter(option => selectedIds.includes(option.id));

  const showAddButton = onAddNew && searchQuery.trim() && filteredOptions.length === 0;

  return (
    <div className="searchable-multi-select" ref={containerRef}>
      {label && <label className="multi-select-label">{label}</label>}
      {helpText && <p className="form-help-text">{helpText}</p>}
      
      {selectedOptions.length > 0 && (
        <div className="selected-items">
          {selectedOptions.map(option => (
            <div key={option.id} className="selected-item">
              <span>{option.name}</span>
              <button
                type="button"
                onClick={() => handleToggle(option.id)}
                className="remove-selected"
              >
                ×
              </button>
            </div>
          ))}
        </div>
      )}

      <div className="search-container">
        <input
          type="text"
          value={searchQuery}
          onChange={(e) => {
            setSearchQuery(e.target.value);
            setIsOpen(true);
          }}
          onFocus={() => setIsOpen(true)}
          onKeyDown={handleKeyDown}
          placeholder={placeholder || t('common.search')}
          className="search-input"
        />
        {searchQuery && (
          <button
            type="button"
            onClick={() => setSearchQuery('')}
            className="clear-search"
          >
            ×
          </button>
        )}
      </div>

      {isOpen && (
        <div className="dropdown-menu">
          {filteredOptions.map(option => (
            <label key={option.id} className="dropdown-item">
              <input
                type="checkbox"
                checked={selectedIds.includes(option.id)}
                onChange={() => handleToggle(option.id)}
              />
              <span>{option.name}</span>
            </label>
          ))}
          {showAddButton && (
            <button
              type="button"
              onClick={handleAddNew}
              className="add-new-button"
            >
              {t('events.addToList', { name: searchQuery })}
            </button>
          )}
        </div>
      )}
    </div>
  );
};

export default SearchableMultiSelect;
