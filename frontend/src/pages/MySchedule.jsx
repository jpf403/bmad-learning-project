import { useEffect, useRef, useState } from 'react'
import { useAuth } from '../context/AuthContext'
import { getSchedule, getBarbers, cancelAppointment } from '../api/BookingApi'
import {
  formatTimeLabel,
  formatDateLabel,
  addWeekdays,
  isWeekend,
  formatDateHeader,
} from '../utils/FormatSchedule'
import ConfirmPopup from '../components/ConfirmPopup'
import Button from '../components/Button'
import SelectDropdown from '../components/SelectDropdown'
import './MySchedule.css'

export default function MySchedule() {
  const { user } = useAuth()

  const [date, setDate] = useState(null)
  const [slots, setSlots] = useState([])
  const [loading, setLoading] = useState(true)
  const [scheduleError, setScheduleError] = useState('')
  const [cancelTarget, setCancelTarget] = useState(null)
  const [cancelError, setCancelError] = useState('')
  const [cancellingId, setCancellingId] = useState(null)
  const [barbers, setBarbers] = useState([])
  const [barbersLoading, setBarbersLoading] = useState(true)
  const [barbersError, setBarbersError] = useState('')
  const [barberId, setBarberId] = useState(null)
  const isMountedRef = useRef(true)
  // Tracks the date most recently *requested*, independent of whether that
  // request succeeded -- `date` state only ever updates on success, so a
  // failed nav-arrow click leaves `date` at the old value. "Try again" must
  // retry the date the user was navigating to, not the one still displayed.
  const attemptedDateRef = useRef(null)
  // Bumped at the start of every loadDate() call. If a newer call starts
  // before an older one's fetch resolves (e.g. two rapid nav-arrow clicks),
  // the older call's captured id no longer matches by the time it resolves,
  // so its result is discarded regardless of which one settles first.
  const requestIdRef = useRef(0)

  useEffect(() => {
    // StrictMode double-invokes this effect in dev (mount -> cleanup -> mount
    // again) to catch missing cleanup. Without resetting to `true` here, the
    // synthetic first cleanup would leave this permanently `false`, and every
    // loadDate() call after that would silently bail before setLoading(false).
    isMountedRef.current = true
    return () => {
      isMountedRef.current = false
    }
  }, [])

  async function fetchSchedule(explicitDate, explicitBarberId) {
    const result = await getSchedule(
      user.accessToken,
      explicitDate,
      explicitBarberId,
    )
    if (result.ok) {
      return {
        date: result.schedule.date,
        slots: result.schedule.slots,
        errorMessage: '',
      }
    }
    return {
      date: null,
      slots: [],
      errorMessage: 'Could not load your schedule. Please try again.',
    }
  }

  async function loadDate(explicitDate, explicitBarberId = barberId) {
    attemptedDateRef.current = explicitDate
    const requestId = ++requestIdRef.current
    setLoading(true)
    const result = await fetchSchedule(explicitDate, explicitBarberId)
    if (!isMountedRef.current || requestId !== requestIdRef.current) {
      return
    }
    setLoading(false)
    if (result.errorMessage) {
      setScheduleError(result.errorMessage)
    } else {
      setDate(result.date)
      setSlots(result.slots)
      setScheduleError('')
    }
  }

  async function fetchBarbers() {
    const result = await getBarbers(user.accessToken)
    if (result.ok) {
      return { barbers: result.barbers, errorMessage: '' }
    }
    return {
      barbers: [],
      errorMessage: 'Could not load barbers. Please try again.',
    }
  }

  useEffect(() => {
    if (user.role !== 'Barber') {
      return
    }
    // Uses its own per-invocation `cancelled` flag rather than `loadDate`'s
    // shared `isMountedRef` -- under StrictMode's dev-only double-invoke,
    // isMountedRef is back to `true` by the second invocation, so it can't
    // stop two concurrent loadDate(null) calls from both applying state.
    // `cancelled` is scoped to just the invocation it belongs to, so only
    // the second (current) invocation's result is ever applied.
    let cancelled = false

    async function load() {
      const result = await fetchSchedule(null)
      if (cancelled) {
        return
      }
      setLoading(false)
      if (result.errorMessage) {
        setScheduleError(result.errorMessage)
      } else {
        setDate(result.date)
        setSlots(result.slots)
        setScheduleError('')
      }
    }

    load()
    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user.accessToken, user.role])

  useEffect(() => {
    if (user.role !== 'Admin') {
      return
    }
    // Own per-invocation `cancelled` flag, mirroring the Barber-only effect
    // above, for the same StrictMode-remount reason.
    let cancelled = false

    async function load() {
      const { barbers: loadedBarbers, errorMessage } = await fetchBarbers()
      if (cancelled) return
      setBarbersLoading(false)
      if (errorMessage) {
        setBarbersError(errorMessage)
        return
      }
      setBarbers(loadedBarbers)
      setBarbersError('')
      if (loadedBarbers.length === 0) {
        return
      }
      const firstId = loadedBarbers[0].id
      setBarberId(firstId)
      const scheduleResult = await fetchSchedule(null, firstId)
      if (cancelled) return
      setLoading(false)
      if (scheduleResult.errorMessage) {
        setScheduleError(scheduleResult.errorMessage)
      } else {
        setDate(scheduleResult.date)
        setSlots(scheduleResult.slots)
        setScheduleError('')
      }
    }

    load()
    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user.accessToken, user.role])

  function handleBarberChange(newBarberId) {
    const id = Number(newBarberId)
    setBarberId(id)
    setCancelTarget(null)
    setCancelError('')
    loadDate(date, id)
  }

  async function retryLoadBarbers() {
    setBarbersLoading(true)
    const { barbers: loadedBarbers, errorMessage } = await fetchBarbers()
    if (!isMountedRef.current) return
    setBarbersLoading(false)
    if (errorMessage) {
      setBarbersError(errorMessage)
      return
    }
    setBarbers(loadedBarbers)
    setBarbersError('')
    if (loadedBarbers.length > 0) {
      const firstId = loadedBarbers[0].id
      setBarberId(firstId)
      await loadDate(null, firstId)
    }
  }

  async function handleCancelConfirmed() {
    const target = cancelTarget
    if (!target || cancellingId !== null) {
      return
    }
    setCancellingId(target.id)
    setCancelError('')
    const result = await cancelAppointment(user.accessToken, target.id)
    if (!isMountedRef.current) {
      return
    }

    if (result.ok) {
      await loadDate(date)
      setCancellingId(null)
      return
    }
    if (result.status === 409) {
      setCancelError(
        result.problem?.title ?? 'This appointment has already been cancelled.',
      )
      await loadDate(date)
      setCancellingId(null)
      return
    }
    setCancelError('Something went wrong. Please try again.')
    setCancellingId(null)
  }

  if (user.role !== 'Barber' && user.role !== 'Admin') {
    return (
      <div className="my-schedule">
        <h1 className="my-schedule__title">My Schedule</h1>
        <p className="my-schedule__loading">
          Schedule view is not available for this account.
        </p>
      </div>
    )
  }

  if (user.role === 'Admin') {
    if (barbersLoading) {
      return (
        <div className="my-schedule">
          <h1 className="my-schedule__title">My Schedule</h1>
          <p className="my-schedule__loading">Loading…</p>
        </div>
      )
    }
    if (barbersError) {
      return (
        <div className="my-schedule">
          <h1 className="my-schedule__title">My Schedule</h1>
          <div className="my-schedule__error-state">
            <p className="my-schedule__error">{barbersError}</p>
            <Button variant="secondary" onClick={retryLoadBarbers}>
              Try again
            </Button>
          </div>
        </div>
      )
    }
    if (barbers.length === 0) {
      return (
        <div className="my-schedule">
          <h1 className="my-schedule__title">My Schedule</h1>
          <p className="my-schedule__loading">No barbers available.</p>
        </div>
      )
    }
  }

  const weekend = date !== null && isWeekend(date)

  return (
    <div
      className={`my-schedule${user.role === 'Admin' ? ' my-schedule--admin' : ''}`}
    >
      <h1 className="my-schedule__title">My Schedule</h1>

      {loading ? (
        <p className="my-schedule__loading">Loading…</p>
      ) : scheduleError ? (
        <div className="my-schedule__error-state">
          <p className="my-schedule__error">{scheduleError}</p>
          <Button
            variant="secondary"
            onClick={() => loadDate(attemptedDateRef.current)}
          >
            Try again
          </Button>
        </div>
      ) : (
        <>
          <div className="date-header-row">
            {user.role === 'Admin' ? (
              <>
                <div className="date-nav-group">
                  <button
                    type="button"
                    className="date-nav-arrow"
                    aria-label="Previous day"
                    disabled={cancellingId !== null}
                    onClick={() => {
                      setCancelTarget(null)
                      setCancelError('')
                      loadDate(addWeekdays(date, -1))
                    }}
                  >
                    &#8249;
                  </button>
                  <h2 className="date-title">{formatDateHeader(date)}</h2>
                  <button
                    type="button"
                    className="date-nav-arrow"
                    aria-label="Next day"
                    disabled={cancellingId !== null}
                    onClick={() => {
                      setCancelTarget(null)
                      setCancelError('')
                      loadDate(addWeekdays(date, 1))
                    }}
                  >
                    &#8250;
                  </button>
                </div>
                <SelectDropdown
                  variant="admin-barber"
                  ariaLabel="Select barber"
                  value={String(barberId)}
                  onChange={handleBarberChange}
                  disabled={cancellingId !== null}
                  options={barbers.map((barber) => ({
                    value: String(barber.id),
                    label: `${barber.firstName} ${barber.lastName}`,
                  }))}
                />
              </>
            ) : (
              <>
                <button
                  type="button"
                  className="date-nav-arrow"
                  aria-label="Previous day"
                  disabled={cancellingId !== null}
                  onClick={() => {
                    setCancelTarget(null)
                    setCancelError('')
                    loadDate(addWeekdays(date, -1))
                  }}
                >
                  &#8249;
                </button>
                <h2 className="date-title">{formatDateHeader(date)}</h2>
                <button
                  type="button"
                  className="date-nav-arrow"
                  aria-label="Next day"
                  disabled={cancellingId !== null}
                  onClick={() => {
                    setCancelTarget(null)
                    setCancelError('')
                    loadDate(addWeekdays(date, 1))
                  }}
                >
                  &#8250;
                </button>
              </>
            )}
          </div>

          {weekend ? (
            <p className="my-schedule__closed">
              Closed — the shop is not open on weekends.
            </p>
          ) : (
            <div className="my-schedule__slot-list">
              {slots.map((slot) =>
                slot.appointment ? (
                  <div className="slot-row slot-booked" key={slot.startTime}>
                    <span className="slot-time">
                      {formatTimeLabel(slot.startTime)}
                    </span>
                    <span className="slot-name">
                      {slot.appointment.customerName}
                    </span>
                    {slot.appointment.finished ? (
                      <span className="slot-status">Finished</span>
                    ) : (
                      <Button
                        variant="destructive"
                        disabled={cancellingId !== null}
                        onClick={() => {
                          setCancelError('')
                          setCancelTarget(slot.appointment)
                        }}
                      >
                        Cancel
                      </Button>
                    )}
                  </div>
                ) : (
                  <div className="slot-row slot-open" key={slot.startTime}>
                    <span className="slot-time">
                      {formatTimeLabel(slot.startTime)}
                    </span>
                    <span className="slot-status">Available</span>
                  </div>
                ),
              )}
            </div>
          )}
          {cancelError && <p className="my-schedule__error">{cancelError}</p>}
        </>
      )}

      <ConfirmPopup
        open={cancelTarget !== null}
        onOpenChange={(open) => !open && setCancelTarget(null)}
        title="Cancel this appointment?"
        message={
          cancelTarget &&
          `${cancelTarget.customerName} — ${formatTimeLabel(cancelTarget.startTime)}, ${formatDateLabel(cancelTarget.date)}. This cannot be undone.`
        }
        destructive
        confirmLabel="Confirm"
        onConfirm={handleCancelConfirmed}
      />
    </div>
  )
}
