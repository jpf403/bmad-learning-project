import { API_BASE_URL } from './ApiConfig'

export async function registerAccount({
  email,
  password,
  firstName,
  lastName,
}) {
  let response
  try {
    response = await fetch(`${API_BASE_URL}/api/auth/register`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password, firstName, lastName }),
    })
  } catch {
    return { ok: false, status: null, problem: null }
  }

  if (response.ok) {
    return { ok: true }
  }

  const problem = await response.json().catch(() => null)
  return { ok: false, status: response.status, problem }
}

export async function loginAccount({ email, password }) {
  let response
  try {
    response = await fetch(`${API_BASE_URL}/api/auth/login`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    })
  } catch {
    return { ok: false, status: null, problem: null }
  }

  const body = await response.json().catch(() => null)
  if (response.ok) {
    return { ok: true, session: body }
  }
  return { ok: false, status: response.status, problem: body }
}

export async function logoutAccount(accessToken) {
  try {
    await fetch(`${API_BASE_URL}/api/auth/logout`, {
      method: 'POST',
      credentials: 'include',
      headers: { Authorization: `Bearer ${accessToken}` },
    })
  } catch {
    // best-effort: caller clears local session regardless of network outcome
  }
}
