import { Link, useLocation, useNavigate } from 'react-router'
import * as DropdownMenu from '@radix-ui/react-dropdown-menu'
import Button from './Button'
import { useAuth } from '../context/AuthContext'
import { logoutAccount } from '../api/AuthApi'
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
  const { user, logout } = useAuth()

  const handleLogout = async () => {
    await logoutAccount(user.accessToken)
    logout()
    navigate('/')
  }

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
        {user ? (
          <DropdownMenu.Root>
            <DropdownMenu.Trigger asChild>
              <button
                className="nav-bar__profile-button"
                aria-label="Account menu"
              >
                <svg
                  viewBox="0 0 24 24"
                  fill="currentColor"
                  aria-hidden="true"
                  className="nav-bar__profile-icon"
                >
                  <path d="M12 12a5 5 0 1 0 0-10 5 5 0 0 0 0 10Zm0 2c-4.42 0-8 2.24-8 5v1a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1v-1c0-2.76-3.58-5-8-5Z" />
                </svg>
              </button>
            </DropdownMenu.Trigger>
            <DropdownMenu.Portal>
              <DropdownMenu.Content
                className="nav-bar__dropdown"
                align="end"
                sideOffset={8}
              >
                <DropdownMenu.Item
                  className="nav-bar__dropdown-item"
                  onSelect={() => navigate('/account')}
                >
                  Account
                </DropdownMenu.Item>
                <DropdownMenu.Item
                  className="nav-bar__dropdown-item"
                  onSelect={handleLogout}
                >
                  Logout
                </DropdownMenu.Item>
              </DropdownMenu.Content>
            </DropdownMenu.Portal>
          </DropdownMenu.Root>
        ) : (
          <>
            <Button variant="secondary" onClick={() => navigate('/login')}>
              Sign In
            </Button>
            <Button variant="primary" onClick={() => navigate('/register')}>
              Register
            </Button>
          </>
        )}
      </div>
    </nav>
  )
}
