import { useState, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { roomApi } from '../services/api'
import { useDashboardHub } from '../hooks/useDashboardHub'
import RoomButton from '../components/RoomButton'
import RoomSummaryBar from '../components/RoomSummaryBar'
import type { Room, RoomSummary, RoomStatus } from '../types'

const MAX_ROOMS = 61

export default function DashboardPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  // Local blink state (BR-25)
  const [blinkMap, setBlinkMap] = useState<Record<number, boolean>>({})
  const [blinkEnabled, setBlinkEnabled] = useState(true)

  const { data: rooms = [], isLoading } = useQuery({
    queryKey: ['rooms'],
    queryFn: roomApi.list,
    refetchInterval: 30_000, // fallback polling if SignalR unavailable
  })

  const { data: summary } = useQuery({
    queryKey: ['rooms', 'summary'],
    queryFn: roomApi.summary,
    refetchInterval: 30_000,
  })

  // Real-time updates via SignalR (BR-25)
  const handleRoomStatusChanged = useCallback((roomId: number, _status: string, shouldBlink: boolean) => {
    queryClient.invalidateQueries({ queryKey: ['rooms'] })
    queryClient.invalidateQueries({ queryKey: ['rooms', 'summary'] })
    if (shouldBlink) {
      setBlinkMap(prev => ({ ...prev, [roomId]: true }))
    }
  }, [queryClient])

  const handleSummaryChanged = useCallback((newSummary: RoomSummary) => {
    queryClient.setQueryData(['rooms', 'summary'], newSummary)
  }, [queryClient])

  useDashboardHub(handleRoomStatusChanged, handleSummaryChanged)

  const roomMap = Object.fromEntries(rooms.map(r => [r.id, r]))

  const handleRoomClick = (roomIndex: number) => {
    const room = roomMap[roomIndex]
    if (!room) {
      toast.error(`Room ${roomIndex} not configured`)
      return
    }
    // BR-16: Block booking on Maintenance rooms
    if (room.roomStatus === 'Maintenance') {
      toast.error('Room is under Maintenance. Please choose another room.')
      return
    }
    navigate(`/booking/${roomIndex}${room.bookingId ? `/${room.bookingId}` : ''}`)
  }

  // Group rooms by level (1=1-11, 2=12-33, 3=34-44, 4=45-55+)
  const levels = [
    { label: 'Level 4', range: [45, 61] },
    { label: 'Level 3', range: [34, 44] },
    { label: 'Level 2', range: [12, 33] },
    { label: 'Level 1', range: [1, 11] },
  ] as const

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <h2 style={{ fontSize: 18, fontWeight: 700 }}>Room Dashboard</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          <button
            className="btn-ghost"
            onClick={() => setBlinkEnabled(b => !b)}
            style={{ fontSize: 12 }}
          >
            {blinkEnabled ? '🔔 Blink ON' : '🔕 Blink OFF'}
          </button>
          <button className="btn-ghost" style={{ fontSize: 12 }}
            onClick={() => { queryClient.invalidateQueries({ queryKey: ['rooms'] }) }}>
            ↻ Refresh
          </button>
        </div>
      </div>

      {/* Summary bar */}
      <RoomSummaryBar summary={summary ?? { open: 0, booked: 0, occupied: 0, housekeeping: 0, maintenance: 0 }} />

      {isLoading && <p style={{ color: 'var(--color-text-dim)', padding: 24 }}>Loading rooms…</p>}

      {/* Room grid by level */}
      {levels.map(({ label, range }) => {
        const levelRooms = Array.from({ length: range[1] - range[0] + 1 }, (_, i) => range[0] + i)
        return (
          <div key={label} style={{ marginBottom: 16 }}>
            <div style={{ fontWeight: 700, marginBottom: 6, color: 'var(--color-text-dim)', fontSize: 13 }}>
              {label}
            </div>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
              {levelRooms.map(idx => (
                <RoomButton
                  key={idx}
                  room={roomMap[idx] ?? null}
                  index={idx}
                  blink={blinkEnabled && (blinkMap[idx] ?? false)}
                  onClick={() => handleRoomClick(idx)}
                />
              ))}
            </div>
          </div>
        )
      })}
    </div>
  )
}