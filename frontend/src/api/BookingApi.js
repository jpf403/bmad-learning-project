import { API_BASE_URL } from './ApiConfig'

export async function getBarbers(accessToken) {
  let response
  try {
    response = await fetch(`${API_BASE_URL}/api/booking/barbers`, {
      credentials: 'include',
      headers: { Authorization: `Bearer ${accessToken}` },
    })
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
  return { ok: true, barbers: body }
}

export async function getAvailability(accessToken, barberId, date) {
  let response
  try {
    response = await fetch(
      `${API_BASE_URL}/api/booking/availability?barberId=${barberId}&date=${date}`,
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
  return { ok: true, slots: body }
}

export async function createBooking(
  accessToken,
  { barberId, date, startTime },
) {
  let response
  try {
    response = await fetch(`${API_BASE_URL}/api/booking`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify({ barberId, date, startTime }),
    })
  } catch {
    return { ok: false, status: null }
  }

  const body = await response.json().catch(() => null)
  if (!response.ok) {
    return { ok: false, status: response.status, problem: body }
  }
  if (body === null) {
    return { ok: false, status: null }
  }
  return { ok: true, confirmation: body }
}
