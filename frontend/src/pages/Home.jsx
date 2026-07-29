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
            aria-hidden="true"
            viewBox="0 0 260 260"
            width="140"
            height="140"
            fill="none"
          >
            {/* Comb: straight bar with teeth, rotated to cross the scissors like an X */}
            <g transform="rotate(45 130 130)">
              <rect
                x="60"
                y="122"
                width="140"
                height="16"
                fill="currentColor"
              />
              <rect x="66" y="138" width="6" height="26" fill="currentColor" />
              <rect x="82" y="138" width="6" height="26" fill="currentColor" />
              <rect x="98" y="138" width="6" height="26" fill="currentColor" />
              <rect x="114" y="138" width="6" height="26" fill="currentColor" />
              <rect x="130" y="138" width="6" height="26" fill="currentColor" />
              <rect x="146" y="138" width="6" height="26" fill="currentColor" />
              <rect x="162" y="138" width="6" height="26" fill="currentColor" />
              <rect x="178" y="138" width="6" height="26" fill="currentColor" />
            </g>
            {/* Scissors: two blade/handle assemblies crossed with the comb */}
            <g transform="rotate(-45 130 130)">
              <line
                x1="130"
                y1="130"
                x2="70"
                y2="40"
                stroke="currentColor"
                strokeWidth="8"
                strokeLinecap="square"
              />
              <line
                x1="130"
                y1="130"
                x2="190"
                y2="40"
                stroke="currentColor"
                strokeWidth="8"
                strokeLinecap="square"
              />
              <circle
                cx="70"
                cy="40"
                r="14"
                fill="none"
                stroke="currentColor"
                strokeWidth="8"
              />
              <circle
                cx="190"
                cy="40"
                r="14"
                fill="none"
                stroke="currentColor"
                strokeWidth="8"
              />
              <line
                x1="130"
                y1="130"
                x2="70"
                y2="220"
                stroke="currentColor"
                strokeWidth="8"
                strokeLinecap="square"
              />
              <line
                x1="130"
                y1="130"
                x2="190"
                y2="220"
                stroke="currentColor"
                strokeWidth="8"
                strokeLinecap="square"
              />
              <circle cx="130" cy="130" r="7" fill="currentColor" />
            </g>
          </svg>
        </div>
      </section>
    </div>
  )
}
