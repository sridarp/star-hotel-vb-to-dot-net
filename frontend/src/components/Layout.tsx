import { Outlet, NavLink, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'

export default function Layout() {
  const { userId, userName, userGroup, logout } = useAuthStore()
  const navigate = useNavigate()

  const handleLogout = () => {
    logout()
    navigate('/login')
  }

  const groupLabel = { 1: 'Administrator', 2: 'Manager', 3: 'Supervisor', 4: 'Clerk' }[userGroup ?? 4] ?? 'User'

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      {/* Header */}
      <header style={{
        background: '#404040',
        borderBottom: '1px solid var(--color-border)',
        padding: '0 24px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        height: 56,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 24 }}>
          <span style={{ fontSize: 18, fontWeight: 700 }}>⭐ Star Hotel</span>
          <nav style={{ display: 'flex', gap: 8 }}>
            {[
              { to: '/dashboard', label: 'Dashboard' },
              { to: '/reports',   label: 'Reports' },
              ...(userGroup === 1 ? [{ to: '/users', label: 'Users' }] : []),
            ].map(({ to, label }) => (
              <NavLink
                key={to}
                to={to}
                style={({ isActive }) => ({
                  color: isActive ? '#fff' : 'var(--color-text-dim)',
                  padding: '4px 12px',
                  borderRadius: 4,
                  fontWeight: isActive ? 700 : 400,
                  background: isActive ? 'rgba(255,255,255,0.1)' : 'transparent',
                })}
              >
                {label}
              </NavLink>
            ))}
          </nav>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
          <span style={{ fontSize: 13, color: 'var(--color-text-dim)' }}>
            <span style={{ color: '#ff8080', fontWeight: 700 }}>{userId}</span>
            {' '}· {groupLabel}
          </span>
          <button className="btn-ghost" onClick={handleLogout} style={{ fontSize: 12 }}>
            Sign Out
          </button>
        </div>
      </header>

      {/* Main content */}
      <main style={{ flex: 1, padding: '20px 24px', maxWidth: 1400, width: '100%', margin: '0 auto' }}>
        <Outlet />
      </main>
    </div>
  )
}