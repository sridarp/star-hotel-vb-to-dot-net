import axios, { type AxiosInstance } from 'axios'
import { useAuthStore } from '../store/authStore'

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api/v1'

function createApiClient(): AxiosInstance {
  const client = axios.create({ baseURL: BASE_URL })

  client.interceptors.request.use((config) => {
    const token = useAuthStore.getState().token
    if (token) config.headers.Authorization = `Bearer ${token}`
    return config
  })

  client.interceptors.response.use(
    (r) => r,
    (error) => {
      if (error.response?.status === 401) {
        useAuthStore.getState().logout()
        window.location.href = '/login'
      }
      return Promise.reject(error)
    }
  )

  return client
}

export const apiClient = createApiClient()

// ── Booking API ───────────────────────────────────────────────────────────────
import type { Booking, SaveBookingRequest, PriceCalculation } from '../types'

export const bookingApi = {
  list: () => apiClient.get<Booking[]>('/bookings').then(r => r.data),
  get: (id: number) => apiClient.get<Booking>(`/bookings/${id}`).then(r => r.data),
  createTemp: () => apiClient.post<{ bookingId: number; formattedId: string }>('/bookings/temp').then(r => r.data),
  save: (id: number, data: SaveBookingRequest) => apiClient.put<Booking>(`/bookings/${id}`, data).then(r => r.data),
  checkIn: (id: number, checkInTime: string) => apiClient.post<Booking>(`/bookings/${id}/checkin`, { checkInTime }).then(r => r.data),
  checkOut: (id: number, checkOutTime: string, refund: number) =>
    apiClient.post<Booking>(`/bookings/${id}/checkout`, { checkOutTime, refund }).then(r => r.data),
  void: (id: number) => apiClient.post<{ bookingId: number; active: boolean }>(`/bookings/${id}/void`).then(r => r.data),
  temporaryReceiptUrl: (id: number) => `${BASE_URL}/bookings/${id}/receipt/temporary`,
  officialReceiptUrl: (id: number) => `${BASE_URL}/bookings/${id}/receipt/official`,
  calculate: (data: PriceCalculation) => apiClient.post<PriceCalculation>('/bookings/calculate', data).then(r => r.data),
}

// ── Room API ──────────────────────────────────────────────────────────────────
import type { Room, RoomSummary } from '../types'

export const roomApi = {
  list: () => apiClient.get<Room[]>('/rooms').then(r => r.data),
  get: (id: number) => apiClient.get<Room>(`/rooms/${id}`).then(r => r.data),
  summary: () => apiClient.get<RoomSummary>('/rooms/summary').then(r => r.data),
  changeStatus: (id: number, status: string) =>
    apiClient.patch<{ roomId: number; status: string }>(`/rooms/${id}/status`, { status }).then(r => r.data),
  types: () => apiClient.get<Array<{ id: number; typeShortName: string }>>('/rooms/types').then(r => r.data),
}

// ── User API ──────────────────────────────────────────────────────────────────
import type { User } from '../types'

export const userApi = {
  me: () => apiClient.get<User>('/users/me').then(r => r.data),
  checkAccess: (moduleId: number) =>
    apiClient.get<{ moduleId: number; hasAccess: boolean }>(`/users/me/access/${moduleId}`).then(r => r.data),
  list: () => apiClient.get<User[]>('/users').then(r => r.data),
  get: (userId: string) => apiClient.get<User>(`/users/${userId}`).then(r => r.data),
}

// ── Auth API (local JWT for dev / Entra ID for prod) ─────────────────────────
export const authApi = {
  login: (userId: string, password: string) =>
    apiClient.post<{ accessToken: string; userId: string; userName: string; userGroup: number; expiresAt: string }>(
      '/auth/login', { userId, password }
    ).then(r => r.data),
}