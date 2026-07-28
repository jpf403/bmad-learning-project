import Button from './Button'
import './NavBar.css'

const LINKS = [
  'Home',
  'Schedule Appointment',
  'About',
  'My Schedule',
  'Admin Panel',
]

export default function NavBar() {
  return (
    <nav className="nav-bar">
      <span className="nav-bar__logo">Fake Barbershop</span>
      <ul className="nav-bar__links">
        {LINKS.map((label) => (
          <li key={label}>
            <a className="nav-bar__link" href="#">
              {label}
            </a>
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
