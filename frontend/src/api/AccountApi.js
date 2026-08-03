import { API_BASE_URL } from './ApiConfig'

export async function updateAccount(
  accessToken,
  { firstName, lastName, newPassword },
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
      }),
    })
  } catch {
    return { ok: false, status: null }
  }

  const body = await response.json().catch(() => null)
  if (!response.ok) {
    return { ok: false, status: response.status, problem: body }
  }
  return { ok: true, identity: body }
}
