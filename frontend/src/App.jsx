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
          </Routes>
        </main>

        <Footer />
      </div>
    </AuthProvider>
  )
}

export default App
