import { useEffect, useState } from 'react'
import { useAuth } from '../context/AuthContext'
import { getBarbers, getAvailability, createBooking } from '../api/BookingApi'
import { formatTimeLabel } from '../utils/FormatSchedule'
import FormSection from '../components/FormSection'
import Calendar from '../components/Calendar'
import SelectDropdown from '../components/SelectDropdown'
import ConfirmationScreen from '../components/ConfirmationScreen'
import Button from '../components/Button'
import './ScheduleAppointment.css'

export default function ScheduleAppointment() {
  const { user } = useAuth()

  const [barbers, setBarbers] = useState([])
  const [barbersLoading, setBarbersLoading] = useState(true)
  const [barberId, setBarberId] = useState('')
  const [date, setDate] = useState('')
  const [availableSlots, setAvailableSlots] = useState([])
  const [availabilityLoading, setAvailabilityLoading] = useState(false)
  const [startTime, setStartTime] = useState('')
  const [formError, setFormError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [confirmation, setConfirmation] = useState(null)

  // Reset the time selection whenever barber or date changes, computed
  // during render (React's documented alternative to an effect for
  // deriving state from a prop change) rather than as a setState call
  // inside an effect body.
  const [selectionKey, setSelectionKey] = useState('')
  const currentSelectionKey = `${barberId}|${date}`
  if (selectionKey !== currentSelectionKey) {
    setSelectionKey(currentSelectionKey)
    setStartTime('')
    setAvailableSlots([])
  }

  useEffect(() => {
    let cancelled = false

    async function loadBarbers() {
      const result = await getBarbers(user.accessToken)
      if (cancelled) {
        return
      }
      if (result.ok) {
        setBarbers(result.barbers)
      }
      setBarbersLoading(false)
    }

    loadBarbers()
    return () => {
      cancelled = true
    }
  }, [user.accessToken])

  useEffect(() => {
    if (!barberId || !date) {
      return
    }

    let cancelled = false

    async function loadAvailability() {
      setAvailabilityLoading(true)
      const result = await getAvailability(user.accessToken, barberId, date)
      if (cancelled) {
        return
      }
      setAvailabilityLoading(false)
      setAvailableSlots(result.ok ? result.slots : [])
    }

    loadAvailability()
    return () => {
      cancelled = true
    }
  }, [barberId, date, user.accessToken])

  async function handleSubmit(event) {
    event.preventDefault()

    if (isSubmitting) {
      return
    }

    setFormError('')
    setIsSubmitting(true)

    const result = await createBooking(user.accessToken, {
      barberId: Number(barberId),
      date,
      startTime,
    })

    setIsSubmitting(false)

    if (result.ok) {
      setConfirmation(result.confirmation)
      return
    }

    if (result.status === 409) {
      setFormError('That time is no longer available. Choose another.')
      setStartTime('')
      setAvailabilityLoading(true)
      const refreshed = await getAvailability(user.accessToken, barberId, date)
      setAvailabilityLoading(false)
      setAvailableSlots(refreshed.ok ? refreshed.slots : [])
      return
    }

    setFormError('Something went wrong. Please try again.')
  }

  if (confirmation) {
    return (
      <ConfirmationScreen
        barberName={confirmation.barberName}
        date={confirmation.date}
        startTime={confirmation.startTime}
      />
    )
  }

  return (
    <div className="schedule-appointment">
      <h1 className="schedule-appointment__title">Schedule Appointment</h1>

      <FormSection>
        {barbersLoading ? (
          <p className="schedule-appointment__loading">Loading…</p>
        ) : (
          <form className="schedule-appointment__form" onSubmit={handleSubmit}>
            <SelectDropdown
              label="Barber"
              value={barberId}
              onChange={setBarberId}
              placeholder="Select a barber"
              emptyMessage="No barbers available"
              options={barbers.map((barber) => ({
                value: String(barber.id),
                label: `${barber.firstName} ${barber.lastName}`,
              }))}
            />

            <Calendar label="Date" value={date} onChange={setDate} />

            {availabilityLoading ? (
              <div className="schedule-appointment__field">
                <span className="input-field__label">Time</span>
                <p className="schedule-appointment__loading">Loading…</p>
              </div>
            ) : (
              <SelectDropdown
                label="Time"
                value={startTime}
                onChange={setStartTime}
                placeholder="Select a time"
                options={availableSlots.map((slot) => ({
                  value: slot,
                  label: formatTimeLabel(slot),
                }))}
              />
            )}

            {formError && (
              <p className="schedule-appointment__form-error">{formError}</p>
            )}

            <Button
              variant="primary"
              type="submit"
              disabled={isSubmitting || !barberId || !date || !startTime}
            >
              Submit
            </Button>
          </form>
        )}
      </FormSection>
    </div>
  )
}
