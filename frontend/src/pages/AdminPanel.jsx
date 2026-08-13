import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import { useAuth } from '../context/AuthContext'
import { searchAccounts } from '../api/AccountApi'
import Input from '../components/Input'
import Button from '../components/Button'
import './AdminPanel.css'

export default function AdminPanel() {
  const navigate = useNavigate()
  const { user, logout } = useAuth()

  const [query, setQuery] = useState('')
  const [searched, setSearched] = useState(false)
  const [accounts, setAccounts] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const lastQueryRef = useRef('')
  const isMountedRef = useRef(true)
  // Bumped at the start of every submitted search. If a newer search starts
  // before an older one's fetch resolves, the older call's captured id no
  // longer matches by the time it resolves, so its result is discarded.
  const requestIdRef = useRef(0)

  useEffect(() => {
    isMountedRef.current = true
    return () => {
      isMountedRef.current = false
    }
  }, [])

  async function runSearch(trimmedQuery) {
    lastQueryRef.current = trimmedQuery
    const requestId = ++requestIdRef.current
    setLoading(true)
    setError('')
    const result = await searchAccounts(user.accessToken, trimmedQuery)
    if (!isMountedRef.current || requestId !== requestIdRef.current) {
      return
    }
    if (result.status === 401) {
      logout()
      navigate('/login', {
        state: { message: 'Your session has expired. Please sign in again.' },
      })
      return
    }
    setLoading(false)
    setSearched(true)
    if (result.ok) {
      setAccounts(result.accounts)
    } else {
      setError('Could not search accounts. Please try again.')
    }
  }

  function handleSubmit(event) {
    event.preventDefault()
    const trimmedQuery = query.trim()
    if (!trimmedQuery) {
      return
    }
    runSearch(trimmedQuery)
  }

  function handleTryAgain() {
    runSearch(lastQueryRef.current)
  }

  return (
    <div className="admin-panel">
      <h1 className="admin-panel__title">Admin Panel</h1>

      <form className="admin-panel__search-form" onSubmit={handleSubmit}>
        <Input
          label="Search"
          placeholder="Name or email"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
        />
        <Button type="submit" disabled={loading}>
          Search
        </Button>
      </form>

      {!searched ? (
        <p className="admin-panel__message">
          Search by name or email to find an account.
        </p>
      ) : loading ? (
        <p className="admin-panel__message">Searching…</p>
      ) : error ? (
        <div className="admin-panel__error-state">
          <p className="admin-panel__error">{error}</p>
          <Button variant="secondary" onClick={handleTryAgain}>
            Try again
          </Button>
        </div>
      ) : accounts.length === 0 ? (
        <p className="admin-panel__message">No accounts match your search.</p>
      ) : (
        <div className="admin-panel__results">
          {accounts.map((account) => (
            <button
              type="button"
              key={account.id}
              className="admin-account-row"
              onClick={() => {
                // Story 3.3 opens the edit popup here; intentional no-op for now.
              }}
            >
              <span className="admin-account-row__name">
                {account.firstName} {account.lastName}
              </span>
              <span className="admin-account-row__email">{account.email}</span>
              <span className="admin-account-row__role">{account.role}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
