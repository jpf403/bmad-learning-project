import { useId } from 'react'
import * as Select from '@radix-ui/react-select'
import './SelectDropdown.css'

export default function SelectDropdown({
  label,
  value,
  onChange,
  options,
  placeholder = 'Select…',
  emptyMessage,
}) {
  const generatedId = useId()

  if (options.length === 0 && emptyMessage) {
    return (
      <div className="select-dropdown">
        {label && <span className="input-field__label">{label}</span>}
        <p className="select-dropdown__empty-message">{emptyMessage}</p>
      </div>
    )
  }

  return (
    <div className="select-dropdown">
      {label && (
        <label className="input-field__label" htmlFor={generatedId}>
          {label}
        </label>
      )}
      <Select.Root value={value ?? undefined} onValueChange={onChange}>
        <Select.Trigger id={generatedId} className="select-dropdown__trigger">
          <Select.Value placeholder={placeholder} />
          <Select.Icon className="select-dropdown__icon">▾</Select.Icon>
        </Select.Trigger>
        <Select.Portal>
          <Select.Content
            className="select-dropdown__content"
            position="popper"
            side="bottom"
            avoidCollisions={false}
            sideOffset={4}
          >
            <Select.Viewport className="select-dropdown__viewport">
              {options.map((option) => (
                <Select.Item
                  key={option.value}
                  value={option.value}
                  className="select-dropdown__item"
                >
                  <Select.ItemText>{option.label}</Select.ItemText>
                </Select.Item>
              ))}
            </Select.Viewport>
          </Select.Content>
        </Select.Portal>
      </Select.Root>
    </div>
  )
}
