import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import toast from 'react-hot-toast'
import { useAuthStore } from '../store/authStore'
import { authApi } from '../services/api'

export default function LoginPage() {
  const [userId, setUserId] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const login = useAuthStore(s => s.login)
  const navigate = useNavigate()

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!userId.trim() || !password.trim()) {
      toast.error('Please enter User ID and Password')
      return
    }

    setLoading(true)
    try {
      // BR-01: User ID uppercased before lookup
      const data = await authApi.login(userId.toUpperCase(), password)
      login(data.accessToken, data.userId, data.userName, data.userGroup)
      navigate('/dashboard')
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { error?: string } } })?.response?.data?.error
        ?? 'Invalid credentials'
      toast.error(msg)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div style={{
      minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'var(--color-bg)', padding: 24
    }}>
      <div className="card" style={{ width: '100%', maxWidth: 380 }}>
        <h1 style={{ fontSize: 24, fontWeight: 700, marginBottom: 8, color: 'var(--color-text)' }}>
          Star Hotel
        </h1>
        <p style={{ color: 'var(--color-text-dim)', marginBottom: 24, fontSize: 13 }}>
          Hotel Room Reservation System
        </p>

        <form onSubmit={handleSubmit}>
          <div style={{ marginBottom: 16 }}>
            <label htmlFor="userId">User ID</label>
            <input
              id="userId"
              type="text"
              value={userId}
              onChange={e => setUserId(e.target.value.toUpperCase())} // BR-01
              placeholder="Enter User ID"
              autoFocus
            />
          </div>

          <div style={{ marginBottom: 24 }}>
            <label htmlFor="password">Password</label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              placeholder="Enter Password"
            />
          </div>

          <button type="submit" className="btn-primary" disabled={loading} style={{ width: '100%' }}>
            {loading ? 'Signing in...' : 'Sign In'}
          </button>
        </form>

        <p style={{ marginTop: 16, color: 'var(--color-text-dim)', fontSize: 11, textAlign: 'center' }}>
          © Star Hotel Booking System 2024
        </p>
      </div>
    </div>
  )
}