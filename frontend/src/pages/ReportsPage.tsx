import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { format } from 'date-fns'
import { apiClient } from '../services/api'

type ReportType = 'daily' | 'shift' | 'shift-all' | 'weekly' | 'monthly' | 'customers'

export default function ReportsPage() {
  const [type, setType] = useState<ReportType>('daily')
  const [date, setDate] = useState(format(new Date(), 'yyyy-MM-dd'))
  const [year, setYear] = useState(new Date().getFullYear())
  const [month, setMonth] = useState(new Date().getMonth() + 1)
  const [searchName, setSearchName] = useState('')

  const queryKey = ['report', type, date, year, month, searchName]

  const { data, isLoading, error, refetch } = useQuery({
    queryKey,
    queryFn: async () => {
      switch (type) {
        case 'daily':    return apiClient.get(`/reports/daily?date=${date}`).then(r => r.data)
        case 'shift':    return apiClient.get(`/reports/shift?date=${date}`).then(r => r.data)
        case 'shift-all':return apiClient.get(`/reports/shift/all?date=${date}`).then(r => r.data)
        case 'weekly':   return apiClient.get(`/reports/weekly?startDate=${date}`).then(r => r.data)
        case 'monthly':  return apiClient.get(`/reports/monthly?year=${year}&month=${month}`).then(r => r.data)
        case 'customers':return apiClient.get(`/reports/customers?name=${searchName}`).then(r => r.data)
        default:         return null
      }
    },
    enabled: false,
  })

  const reportTypes: { value: ReportType; label: string }[] = [
    { value: 'daily',     label: 'Daily Booking Report' },
    { value: 'weekly',    label: 'Weekly Booking Report' },
    { value: 'monthly',   label: 'Monthly Booking Report' },
    { value: 'shift',     label: 'Shift Report (My Staff)' },
    { value: 'shift-all', label: 'Shift Report (All Staff)' },
    { value: 'customers', label: 'Find Customer' },
  ]

  const records: Array<Record<string, unknown>> = data?.records ?? data?.results ?? []

  return (
    <div>
      <h2 style={{ marginBottom: 16, fontSize: 18, fontWeight: 700 }}>Reports</h2>

      <div className="card" style={{ marginBottom: 16 }}>
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <div>
            <label>Report Type</label>
            <select value={type} onChange={e => setType(e.target.value as ReportType)} style={{ width: 240 }}>
              {reportTypes.map(r => <option key={r.value} value={r.value}>{r.label}</option>)}
            </select>
          </div>

          {(type === 'daily' || type === 'shift' || type === 'shift-all' || type === 'weekly') && (
            <div>
              <label>Date</label>
              <input type="date" value={date} onChange={e => setDate(e.target.value)} style={{ width: 160 }} />
            </div>
          )}

          {type === 'monthly' && (
            <>
              <div>
                <label>Year</label>
                <input type="number" value={year} onChange={e => setYear(+e.target.value)} style={{ width: 100 }} />
              </div>
              <div>
                <label>Month</label>
                <select value={month} onChange={e => setMonth(+e.target.value)} style={{ width: 120 }}>
                  {Array.from({ length: 12 }, (_, i) => (
                    <option key={i + 1} value={i + 1}>
                      {new Date(2000, i).toLocaleString('default', { month: 'long' })}
                    </option>
                  ))}
                </select>
              </div>
            </>
          )}

          {type === 'customers' && (
            <div>
              <label>Guest Name</label>
              <input value={searchName} onChange={e => setSearchName(e.target.value)}
                placeholder="Search by name…" style={{ width: 200 }} />
            </div>
          )}

          <button className="btn-primary" onClick={() => refetch()} disabled={isLoading}>
            {isLoading ? 'Loading…' : '🔍 Generate'}
          </button>
        </div>
      </div>

      {error && <p className="error">Failed to load report. Check your access permissions.</p>}

      {records.length > 0 && (
        <div style={{ overflowX: 'auto' }}>
          <div style={{ marginBottom: 8, color: 'var(--color-text-dim)', fontSize: 13 }}>
            {records.length} record(s) found
          </div>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead>
              <tr style={{ background: 'var(--color-bg)', borderBottom: '1px solid var(--color-border)' }}>
                {Object.keys(records[0]).map(k => (
                  <th key={k} style={{ padding: '6px 10px', textAlign: 'left', color: 'var(--color-text-dim)' }}>
                    {k.replace(/([A-Z])/g, ' $1').trim()}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {records.map((row, i) => (
                <tr key={i} style={{ borderBottom: '1px solid var(--color-border)' }}>
                  {Object.values(row).map((cell, j) => (
                    <td key={j} style={{ padding: '6px 10px' }}>
                      {typeof cell === 'string' && cell.includes('T')
                        ? cell.substring(0, 16).replace('T', ' ')
                        : String(cell ?? '')}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {data && records.length === 0 && !isLoading && (
        <p style={{ color: 'var(--color-text-dim)', textAlign: 'center', padding: 32 }}>
          No records found for the selected criteria.
        </p>
      )}
    </div>
  )
}