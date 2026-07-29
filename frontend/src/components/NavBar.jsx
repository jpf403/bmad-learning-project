import { Link, useLocation } from 'react-router'
import Button from './Button'
import './NavBar.css'

const ROUTED_LINKS = [
  { label: 'Home', to: '/' },
  { label: 'About', to: '/about' },
]

const INERT_LINKS = ['Schedule Appointment', 'My Schedule', 'Admin Panel']

export default function NavBar() {
  const location = useLocation()

  return (
    <nav className="nav-bar">
      <span className="nav-bar__logo">Fake Barbershop</span>
      <ul className="nav-bar__links">
        {ROUTED_LINKS.map(({ label, to }) => (
          <li key={label}>
            <Link
              className={
                location.pathname === to
                  ? 'nav-bar__link nav-bar__link--active'
                  : 'nav-bar__link'
              }
              to={to}
            >
              {label}
            </Link>
          </li>
        ))}
        {INERT_LINKS.map((label) => (
          <li key={label}>
            <span className="nav-bar__link nav-bar__link--inert">{label}</span>
          </li>
        ))}
      </ul>
      <div className="nav-bar__actions">
        <Button variant="secondary">Sign In</Button>
        <Button variant="primary">Register</Button>
      </div>
    </nav>
  )
}
