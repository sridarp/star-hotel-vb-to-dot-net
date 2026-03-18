import { useQuery } from '@tanstack/react-query'
import { userApi } from '../services/api'

export default function UsersPage() {
  const { data: users = [], isLoading } = useQuery({
    queryKey: ['users'],
    queryFn: userApi.list,
  })

  const groupLabels: Record<number, string> = { 1: 'Administrator', 2: 'Manager', 3: 'Supervisor', 4: 'Clerk' }

  return (
    <div>
      <h2 style={{ marginBottom: 16, fontSize: 18, fontWeight: 700 }}>User Management</h2>

      {isLoading && <p style={{ color: 'var(--color-text-dim)' }}>Loading users…</p>}

      {!isLoading && (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead>
              <tr style={{ background: 'var(--color-bg)', borderBottom: '1px solid var(--color-border)' }}>
                {['User ID', 'Name', 'Group', 'Idle (s)', 'Login Attempts', 'Blink', 'Active'].map(h => (
                  <th key={h} style={{ padding: '6px 10px', textAlign: 'left', color: 'var(--color-text-dim)' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {users.map(u => (
                <tr key={u.id} style={{ borderBottom: '1px solid var(--color-border)' }}>
                  <td style={{ padding: '6px 10px', fontWeight: 600 }}>{u.userId}</td>
                  <td style={{ padding: '6px 10px' }}>{u.userName}</td>
                  <td style={{ padding: '6px 10px' }}>
                    <span style={{
                      background: u.userGroup === 1 ? '#4c87c9' : 'var(--color-bg)',
                      padding: '2px 8px', borderRadius: 4, fontSize: 11,
                    }}>
                      {groupLabels[u.userGroup] ?? `Group ${u.userGroup}`}
                    </span>
                  </td>
                  <td style={{ padding: '6px 10px' }}>{u.idle}</td>
                  <td style={{ padding: '6px 10px', color: u.loginAttempts > 0 ? '#ff5252' : 'inherit' }}>
                    {u.loginAttempts}
                  </td>
                  <td style={{ padding: '6px 10px' }}>{u.dashboardBlink ? '✅' : '—'}</td>
                  <td style={{ padding: '6px 10px' }}>
                    <span style={{ color: u.active ? 'var(--color-green)' : '#ff5252' }}>
                      {u.active ? 'Active' : '🔒 Locked'}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}