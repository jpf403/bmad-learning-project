import { Routes, Route } from 'react-router'
import { AuthProvider } from './context/AuthContext'
import NavBar from './components/NavBar'
import Footer from './components/Footer'
import RequireRole from './components/RequireRole'
import Home from './pages/Home'
import About from './pages/About'
import Register from './pages/Register'
import Login from './pages/Login'
import Account from './pages/Account'
import ScheduleAppointment from './pages/ScheduleAppointment'
import MySchedule from './pages/MySchedule'
import AdminPanel from './pages/AdminPanel'
import './App.css'

function App() {
  return (
    <AuthProvider>
      <div className="app-shell">
        <NavBar />

        <main>
          <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/about" element={<About />} />
            <Route path="/register" element={<Register />} />
            <Route path="/login" element={<Login />} />
            <Route
              path="/account"
              element={
                <RequireRole roles={['Customer', 'Barber', 'Admin']}>
                  <Account />
                </RequireRole>
              }
            />
            <Route
              path="/schedule-appointment"
              element={
                <RequireRole roles={['Customer', 'Barber', 'Admin']}>
                  <ScheduleAppointment />
                </RequireRole>
              }
            />
            <Route
              path="/my-schedule"
              element={
                <RequireRole roles={['Barber', 'Admin']}>
                  <MySchedule />
                </RequireRole>
              }
            />
            <Route
              path="/admin"
              element={
                <RequireRole roles={['Admin']}>
                  <AdminPanel />
                </RequireRole>
              }
            />
          </Routes>
        </main>

        <Footer />
      </div>
    </AuthProvider>
  )
}

export default App
