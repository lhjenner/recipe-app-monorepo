import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import type { LookupValue } from '../services/ingredientsApi';
import { toTitleCase } from '../titleCase';

interface LookupTypeaheadProps {
  label?: string;
  accessibleLabel: string;
  options: LookupValue[];
  value: string;
  disabled?: boolean;
  onChange: (id: string | null, name: string) => void;
  onCreate: (name: string) => Promise<LookupValue>;
}

function normalize(value: string) {
  return value.trim().replace(/\s+/g, ' ').toLocaleLowerCase();
}

function LookupTypeahead({
  label,
  accessibleLabel,
  options,
  value,
  disabled = false,
  onChange,
  onCreate,
}: LookupTypeaheadProps) {
  const inputId = useId();
  const listId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);
  const [isOpen, setIsOpen] = useState(false);
  const [showAll, setShowAll] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const query = normalize(value);
  const selected = options.find((option) => normalize(option.name) === query);
  const visibleOptions = showAll || !query
    ? options
    : options.filter((option) => normalize(option.name).includes(query));
  const expanded = isOpen && !disabled && visibleOptions.length > 0;
  const optionId = (index: number) => `${listId}-option-${index}`;

  useEffect(() => {
    if (expanded && activeIndex >= 0) {
      document.getElementById(optionId(activeIndex))?.scrollIntoView?.({ block: 'nearest' });
    }
  });

  function open() {
    // Opening from the chevron or keyboard lists everything, like a native select.
    setShowAll(true);
    setActiveIndex(selected ? options.indexOf(selected) : -1);
    setIsOpen(true);
  }

  function choose(option: LookupValue) {
    onChange(option.id, toTitleCase(option.name));
    setIsOpen(false);
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      if (!expanded) {
        open();
      } else {
        setActiveIndex((index) => Math.min(index + 1, visibleOptions.length - 1));
      }
    } else if (event.key === 'ArrowUp' && expanded) {
      event.preventDefault();
      setActiveIndex((index) => Math.max(index - 1, 0));
    } else if (event.key === 'Enter' && expanded && activeIndex >= 0) {
      event.preventDefault();
      choose(visibleOptions[activeIndex]);
    } else if (event.key === 'Escape' && expanded) {
      event.preventDefault();
      setIsOpen(false);
    }
  }

  async function createOption() {
    setIsCreating(true);
    setError(null);
    try {
      const created = await onCreate(value.trim().replace(/\s+/g, ' '));
      onChange(created.id, toTitleCase(created.name));
    } catch (requestError: unknown) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to create this option.');
    } finally {
      setIsCreating(false);
    }
  }

  return (
    <div className="ingredient-lookup-field">
      <div className="ingredient-lookup-control">
        {label && <label htmlFor={inputId}>{label}</label>}
        <div className="combobox">
          <input
            ref={inputRef}
            id={inputId}
            className="combobox-input"
            role="combobox"
            aria-label={accessibleLabel}
            aria-autocomplete="list"
            aria-expanded={expanded}
            aria-controls={expanded ? listId : undefined}
            aria-activedescendant={expanded && activeIndex >= 0 ? optionId(activeIndex) : undefined}
            autoComplete="off"
            disabled={disabled}
            value={value}
            onClick={() => {
              if (!isOpen) {
                open();
              }
            }}
            onKeyDown={onKeyDown}
            onChange={(event) => {
              const nextValue = event.currentTarget.value;
              const match = options.find((option) => normalize(option.name) === normalize(nextValue));
              onChange(match?.id ?? null, nextValue);
              setShowAll(false);
              setActiveIndex(-1);
              setIsOpen(true);
            }}
            onBlur={() => {
              setIsOpen(false);
              const formatted = toTitleCase(value);
              if (formatted !== value) {
                onChange(selected?.id ?? null, formatted);
              }
            }}
          />
          <button
            type="button"
            className="combobox-toggle"
            tabIndex={-1}
            aria-label={`Show ${accessibleLabel} options`}
            disabled={disabled}
            onMouseDown={(event) => event.preventDefault()}
            onClick={() => {
              if (expanded) {
                setIsOpen(false);
              } else {
                inputRef.current?.focus();
                open();
              }
            }}
          />
          {expanded && (
            <ul id={listId} className="combobox-listbox" role="listbox" aria-label={accessibleLabel}>
              {visibleOptions.map((option, index) => (
                <li
                  id={optionId(index)}
                  key={option.id}
                  className={index === activeIndex ? 'combobox-option is-active' : 'combobox-option'}
                  role="option"
                  aria-selected={option.id === selected?.id}
                  onMouseDown={(event) => event.preventDefault()}
                  onMouseEnter={() => setActiveIndex(index)}
                  onClick={() => choose(option)}
                >
                  {toTitleCase(option.name)}
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
      {!disabled && !selected && value.trim() && (
        <button type="button" onClick={() => void createOption()} disabled={isCreating}>
          {isCreating ? 'Creating…' : `Create '${value.trim()}'`}
        </button>
      )}
      {error && <span className="ingredient-field-error" role="alert">{error}</span>}
    </div>
  );
}

export default LookupTypeahead;
