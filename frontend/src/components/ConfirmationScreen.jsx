import { formatTimeLabel, formatDateLabel } from '../utils/FormatSchedule'
import './ConfirmationScreen.css'

export default function ConfirmationScreen({ barberName, date, startTime }) {
  return (
    <div className="confirmation-screen">
      <h1 className="confirmation-screen__title">Appointment Confirmed</h1>
      <p className="confirmation-screen__message">
        {`Appointment booked with ${barberName} at ${formatTimeLabel(startTime)} on ${formatDateLabel(date)}.`}
      </p>
    </div>
  )
}
