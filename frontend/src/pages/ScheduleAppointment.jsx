import { useEffect, useState } from 'react'
import { useAuth } from '../context/AuthContext'
import {
  getBarbers,
  getAvailability,
  createBooking,
  getMyAppointments,
  cancelAppointment,
} from '../api/BookingApi'
import { formatTimeLabel, formatDateLabel } from '../utils/FormatSchedule'
import FormSection from '../components/FormSection'
import Calendar from '../components/Calendar'
import SelectDropdown from '../components/SelectDropdown'
import ConfirmationScreen from '../components/ConfirmationScreen'
import ConfirmPopup from '../components/ConfirmPopup'
import Button from '../components/Button'
import './ScheduleAppointment.css'

export default function ScheduleAppointment() {
  const { user } = useAuth()

  const [barbers, setBarbers] = useState([])
  const [barbersLoading, setBarbersLoading] = useState(true)
  const [barbersError, setBarbersError] = useState('')
  const [barberId, setBarberId] = useState('')
  const [date, setDate] = useState('')
  const [availableSlots, setAvailableSlots] = useState([])
  const [availabilityLoading, setAvailabilityLoading] = useState(false)
  const [availabilityError, setAvailabilityError] = useState('')
  const [startTime, setStartTime] = useState('')
  const [formError, setFormError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [confirmation, setConfirmation] = useState(null)

  const [appointments, setAppointments] = useState([])
  const [appointmentsLoading, setAppointmentsLoading] = useState(true)
  const [appointmentsError, setAppointmentsError] = useState('')
  const [cancelTarget, setCancelTarget] = useState(null)
  const [cancelError, setCancelError] = useState('')

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
    setFormError('')
    setAvailabilityError('')
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
        setBarbersError('')
      } else {
        setBarbersError('Could not load barbers. Please try again.')
      }
      setBarbersLoading(false)
    }

    loadBarbers()
    return () => {
      cancelled = true
    }
  }, [user.accessToken])

  async function loadAppointments() {
    setAppointmentsLoading(true)
    const result = await getMyAppointments(user.accessToken)
    if (result.ok) {
      setAppointments(result.appointments)
      setAppointmentsError('')
    } else {
      setAppointmentsError('Could not load appointments. Please try again.')
    }
    setAppointmentsLoading(false)
  }

  useEffect(() => {
    let cancelled = false

    async function load() {
      const result = await getMyAppointments(user.accessToken)
      if (cancelled) {
        return
      }
      if (result.ok) {
        setAppointments(result.appointments)
        setAppointmentsError('')
      } else {
        setAppointmentsError('Could not load appointments. Please try again.')
      }
      setAppointmentsLoading(false)
    }

    load()
    return () => {
      cancelled = true
    }
  }, [user.accessToken])

  async function handleCancelConfirmed() {
    const target = cancelTarget
    setCancelError('')
    const result = await cancelAppointment(user.accessToken, target.id)

    if (result.ok) {
      await loadAppointments()
      return
    }

    if (result.status === 409) {
      setCancelError('This appointment has already been cancelled.')
      await loadAppointments()
      return
    }

    setCancelError('Something went wrong. Please try again.')
  }

  useEffect(() => {
    if (!barberId || !date) {
      return
    }

    let cancelled = false

    async function loadAvailability() {
      setAvailabilityLoading(true)
      setAvailabilityError('')
      const result = await getAvailability(user.accessToken, barberId, date)
      if (cancelled) {
        return
      }
      setAvailabilityLoading(false)
      if (result.ok) {
        setAvailableSlots(result.slots)
      } else {
        setAvailableSlots([])
        setAvailabilityError(
          'Could not load available times. Please try again.',
        )
      }
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
        ) : barbersError ? (
          <p className="schedule-appointment__form-error">{barbersError}</p>
        ) : (
          <form className="schedule-appointment__form" onSubmit={handleSubmit}>
            <SelectDropdown
              label="Barber"
              value={barberId}
              onChange={setBarberId}
              placeholder="Select a barber"
              emptyMessage="No barbers available"
              disabled={isSubmitting}
              options={barbers.map((barber) => ({
                value: String(barber.id),
                label: `${barber.firstName} ${barber.lastName}`,
              }))}
            />

            <Calendar
              label="Date"
              value={date}
              onChange={setDate}
              disabled={isSubmitting}
            />

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
                emptyMessage={
                  !barberId || !date
                    ? 'Select a barber and date first'
                    : 'No times available'
                }
                disabled={isSubmitting}
                options={availableSlots.map((slot) => ({
                  value: slot,
                  label: formatTimeLabel(slot),
                }))}
              />
            )}

            {availabilityError && (
              <p className="schedule-appointment__form-error">
                {availabilityError}
              </p>
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

      <section className="schedule-appointment__appointments">
        <h2 className="section-title">My Appointments</h2>
        {appointmentsLoading ? (
          <p className="schedule-appointment__loading">Loading…</p>
        ) : appointmentsError ? (
          <p className="schedule-appointment__form-error">
            {appointmentsError}
          </p>
        ) : appointments.length === 0 ? (
          <p>No upcoming appointments.</p>
        ) : (
          appointments.map((appt) => (
            <div className="appt-row" key={appt.id}>
              <div className="appt-info">
                <span className="appt-primary">{appt.barberName}</span>
                <span className="appt-meta">
                  {`${formatTimeLabel(appt.startTime)}, ${formatDateLabel(appt.date)}`}
                </span>
              </div>
              <Button
                variant="destructive"
                onClick={() => setCancelTarget(appt)}
              >
                Cancel
              </Button>
            </div>
          ))
        )}
        {cancelError && (
          <p className="schedule-appointment__form-error">{cancelError}</p>
        )}
      </section>

      <ConfirmPopup
        open={cancelTarget !== null}
        onOpenChange={(open) => !open && setCancelTarget(null)}
        title="Cancel this appointment?"
        message={
          cancelTarget &&
          `${cancelTarget.barberName} — ${formatTimeLabel(cancelTarget.startTime)}, ${formatDateLabel(cancelTarget.date)}. This cannot be undone.`
        }
        destructive
        confirmLabel="Confirm"
        onConfirm={handleCancelConfirmed}
      />
    </div>
  )
}
