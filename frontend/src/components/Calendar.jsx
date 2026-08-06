import { useId, useState } from 'react'
import * as Popover from '@radix-ui/react-popover'
import { DayPicker } from 'react-day-picker'
import './Calendar.css'

const DAYS_AHEAD_LIMIT = 30

function toDateString(date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function fromDateString(value) {
  if (!value) {
    return undefined
  }
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day)
}

function isDisabledDay(date) {
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const maxDate = new Date(today)
  maxDate.setDate(maxDate.getDate() + DAYS_AHEAD_LIMIT)

  const day = new Date(date)
  day.setHours(0, 0, 0, 0)

  const dayOfWeek = day.getDay()
  return day < today || day > maxDate || dayOfWeek === 0 || dayOfWeek === 6
}

export default function Calendar({ label, value, onChange }) {
  const generatedId = useId()
  const [open, setOpen] = useState(false)
  const selected = fromDateString(value)

  function handleSelect(date) {
    if (!date) {
      return
    }
    onChange(toDateString(date))
    setOpen(false)
  }

  return (
    <div className="calendar">
      {label && (
        <label className="input-field__label" htmlFor={generatedId}>
          {label}
        </label>
      )}
      <Popover.Root open={open} onOpenChange={setOpen}>
        <Popover.Trigger asChild>
          <button type="button" id={generatedId} className="calendar-trigger">
            {selected
              ? selected.toLocaleDateString('en-US', {
                  month: 'long',
                  day: 'numeric',
                  year: 'numeric',
                })
              : 'Select a date'}
          </button>
        </Popover.Trigger>
        <Popover.Portal>
          <Popover.Content
            className="calendar-panel"
            align="start"
            sideOffset={4}
          >
            <DayPicker
              mode="single"
              selected={selected}
              onSelect={handleSelect}
              disabled={isDisabledDay}
            />
          </Popover.Content>
        </Popover.Portal>
      </Popover.Root>
    </div>
  )
}
