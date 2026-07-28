import { useState } from 'react'
import NavBar from './components/NavBar'
import Footer from './components/Footer'
import Button from './components/Button'
import Input from './components/Input'
import ConfirmPopup from './components/ConfirmPopup'
import './App.css'

function App() {
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [destructiveConfirmOpen, setDestructiveConfirmOpen] = useState(false)

  return (
    <>
      <NavBar />

      <main className="showcase">
        <h1>Design System Showcase</h1>

        <section>
          <h2>Buttons</h2>
          <div className="showcase__row">
            <Button variant="primary">Primary</Button>
            <Button variant="secondary">Secondary</Button>
            <Button variant="destructive">Destructive</Button>
          </div>
        </section>

        <section>
          <h2>Inputs</h2>
          <div className="showcase__row showcase__row--column">
            <Input label="Email" placeholder="you@example.com" />
            <Input label="Password" type="password" />
            <Input
              label="Confirm Password"
              type="password"
              error="Passwords do not match"
            />
          </div>
        </section>

        <section>
          <h2>Confirm Popups</h2>
          <div className="showcase__row">
            <Button variant="primary" onClick={() => setConfirmOpen(true)}>
              Open Non-Destructive Confirm
            </Button>
            <Button
              variant="destructive"
              onClick={() => setDestructiveConfirmOpen(true)}
            >
              Open Destructive Confirm
            </Button>
          </div>
          <ConfirmPopup
            open={confirmOpen}
            onOpenChange={setConfirmOpen}
            title="Save changes?"
            message="This will update your account details."
            onConfirm={() => setConfirmOpen(false)}
          />
          <ConfirmPopup
            open={destructiveConfirmOpen}
            onOpenChange={setDestructiveConfirmOpen}
            title="Cancel appointment?"
            message="This cannot be undone."
            destructive
            confirmLabel="Confirm"
            onConfirm={() => setDestructiveConfirmOpen(false)}
          />
        </section>
      </main>

      <Footer />
    </>
  )
}

export default App
