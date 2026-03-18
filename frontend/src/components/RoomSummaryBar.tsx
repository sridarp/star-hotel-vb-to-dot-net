import type { RoomSummary } from '../types'
import { ROOM_COLORS } from '../types'

interface Props {
  summary: RoomSummary
}

export default function RoomSummaryBar({ summary }: Props) {
  const items = [
    { label: 'Open',         value: summary.open,         status: 'Open'         },
    { label: 'Booked',       value: summary.booked,       status: 'Booked'       },
    { label: 'Occupied',     value: summary.occupied,     status: 'Occupied'     },
    { label: 'Housekeeping', value: summary.housekeeping, status: 'Housekeeping' },
    { label: 'Maintenance',  value: summary.maintenance,  status: 'Maintenance'  },
  ] as const

  return (
    <div style={{
      display: 'flex',
      gap: 8,
      padding: '8px 0',
      marginBottom: 16,
      flexWrap: 'wrap',
    }}>
      {items.map(({ label, value, status }) => (
        <div
          key={status}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            background: 'var(--color-bg-light)',
            borderRadius: 6,
            padding: '4px 12px',
            fontSize: 12,
            fontWeight: 600,
          }}
        >
          <div style={{
            width: 10,
            height: 10,
            borderRadius: '50%',
            background: ROOM_COLORS[status],
            flexShrink: 0,
          }} />
          <span style={{ color: 'var(--color-text-dim)' }}>{label}:</span>
          <span style={{ fontWeight: 700, fontSize: 14 }}>{value}</span>
        </div>
      ))}
    </div>
  )
}