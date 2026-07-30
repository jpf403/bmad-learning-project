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
