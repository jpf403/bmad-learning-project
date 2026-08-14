import { API_BASE_URL } from './ApiConfig'

export async function updateAccount(
  accessToken,
  { firstName, lastName, newPassword, currentPassword },
) {
  let response
  try {
    response = await fetch(`${API_BASE_URL}/api/account/me`, {
      method: 'PUT',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify({
        firstName,
        lastName,
        newPassword: newPassword || null,
        currentPassword: currentPassword || null,
      }),
    })
  } catch {
    return { ok: false, status: null }
  }

  const body = await response.json().catch(() => null)
  if (!response.ok) {
    return { ok: false, status: response.status, problem: body }
  }
  if (!body) {
    return { ok: false, status: null }
  }
  return { ok: true, identity: body }
}

export async function adminUpdateAccount(
  accessToken,
  accountId,
  { email, firstName, lastName, role, newPassword },
) {
  let response
  try {
    response = await fetch(`${API_BASE_URL}/api/account/${accountId}`, {
      method: 'PUT',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify({
        email,
        firstName,
        lastName,
        role,
        newPassword: newPassword || null,
      }),
    })
  } catch {
    return { ok: false, status: null }
  }

  const body = await response.json().catch(() => null)
  if (!response.ok) {
    return { ok: false, status: response.status, problem: body }
  }
  if (!body) {
    return { ok: false, status: null }
  }
  return { ok: true, account: body }
}

export async function searchAccounts(accessToken, query) {
  const params = new URLSearchParams()
  if (query && query.trim()) {
    params.set('query', query.trim())
  }
  const search = params.toString()

  let response
  try {
    response = await fetch(
      `${API_BASE_URL}/api/account/search${search ? `?${search}` : ''}`,
      {
        credentials: 'include',
        headers: { Authorization: `Bearer ${accessToken}` },
      },
    )
  } catch {
    return { ok: false, status: null }
  }

  const body = await response.json().catch(() => null)
  if (!response.ok || body === null) {
    return {
      ok: false,
      status: response.ok ? null : response.status,
      problem: body,
    }
  }
  return { ok: true, accounts: body }
}
