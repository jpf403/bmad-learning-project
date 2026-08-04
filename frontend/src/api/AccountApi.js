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
