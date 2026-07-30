import { Link, useLocation, useNavigate } from 'react-router'
import Button from './Button'
import './NavBar.css'

const ROUTED_LINKS = [
  { label: 'Home', to: '/' },
  { label: 'About', to: '/about' },
]

const INERT_LINKS = ['Schedule Appointment', 'My Schedule', 'Admin Panel']

function normalizePath(path) {
  return path.toLowerCase().replace(/\/$/, '') || '/'
}

export default function NavBar() {
  const location = useLocation()
  const navigate = useNavigate()
  const currentPath = normalizePath(location.pathname)

  return (
    <nav className="nav-bar">
      <span className="nav-bar__logo">Fake Barbershop</span>
      <ul className="nav-bar__links">
        {ROUTED_LINKS.map(({ label, to }) => (
          <li key={label}>
            <Link
              className={
                currentPath === normalizePath(to)
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
        <Button variant="primary" onClick={() => navigate('/register')}>
          Register
        </Button>
      </div>
    </nav>
  )
}
