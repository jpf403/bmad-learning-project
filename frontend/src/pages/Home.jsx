import { useNavigate } from 'react-router'
import Button from '../components/Button'
import './Home.css'

export default function Home({ isSignedIn = false }) {
  const navigate = useNavigate()

  const handleCtaClick = () => {
    navigate(isSignedIn ? '/schedule-appointment' : '/login')
  }

  return (
    <div className="home">
      <section className="home__hero">
        <div className="home__hero-white">
          <h1 className="home__headline">
            Your next haircut, booked in under a minute.
          </h1>
          <p className="home__tagline">
            Walk-in convenience, without the wait.
          </p>
          <Button variant="primary" onClick={handleCtaClick}>
            Schedule Appointment
          </Button>
        </div>
        <div className="home__hero-teal">
          <svg
            className="home__hero-icon"
            aria-hidden="true"
            viewBox="0 0 100 100"
            width="96"
            height="96"
            fill="none"
            stroke="currentColor"
            strokeWidth="6"
            strokeLinecap="round"
          >
            <line x1="15" y1="15" x2="85" y2="85" />
            <line x1="15" y1="85" x2="85" y2="15" />
          </svg>
        </div>
      </section>
    </div>
  )
}
