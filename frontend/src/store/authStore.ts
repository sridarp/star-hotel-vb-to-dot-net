import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface AuthState {
  token: string | null
  userId: string | null
  userName: string | null
  userGroup: number | null
  isAuthenticated: boolean
  login: (token: string, userId: string, userName: string, userGroup: number) => void
  logout: () => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      userId: null,
      userName: null,
      userGroup: null,
      isAuthenticated: false,
      login: (token, userId, userName, userGroup) =>
        set({ token, userId: userId.toUpperCase(), userName, userGroup, isAuthenticated: true }),
      logout: () =>
        set({ token: null, userId: null, userName: null, userGroup: null, isAuthenticated: false }),
    }),
    { name: 'star-hotel-auth' }
  )
)