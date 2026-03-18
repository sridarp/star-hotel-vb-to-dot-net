import { useEffect, useState } from 'react'
import type { Room, RoomStatus } from '../types'
import { ROOM_COLORS, ROOM_TEXT_COLORS } from '../types'

interface Props {
  room: Room | null
  index: number
  blink: boolean
  onClick: () => void
}

export default function RoomButton({ room, index, blink, onClick }: Props) {
  const [isBlinking, setIsBlinking] = useState(false)

  // Blink animation (BR-25): alternates between status colour and grey
  useEffect(() => {
    if (!blink) { setIsBlinking(false); return }
    const interval = setInterval(() => setIsBlinking(b => !b), 500)
    return () => clearInterval(interval)
  }, [blink])

  if (!room || !room.active) {
    return (
      <button
        onClick={onClick}
        style={{
          width: 90, height: 70,
          background: '#404040',
          color: '#666',
          border: '1px dashed #555',
          borderRadius: 6,
          fontSize: 11,
          cursor: 'pointer',
        }}
        title={`Room ${index} (not configured)`}
      >
        <div>{index.toString().padStart(3, '0')}</div>
        <div style={{ fontSize: 9, marginTop: 2 }}>Not set</div>
      </button>
    )
  }

  const status = room.roomStatus as RoomStatus
  const bgColor = isBlinking ? '#505050' : ROOM_COLORS[status]
  const textColor = isBlinking ? '#aaa' : ROOM_TEXT_COLORS[status]

  return (
    <button
      onClick={onClick}
      style={{
        width: 90,
        height: 70,
        background: bgColor,
        color: textColor,
        border: 'none',
        borderRadius: 6,
        fontSize: 11,
        fontWeight: 700,
        cursor: room.roomStatus === 'Maintenance' ? 'not-allowed' : 'pointer',
        transition: 'background 0.2s',
        lineHeight: 1.3,
        overflow: 'hidden',
        padding: 4,
      }}
      title={`${room.roomShortName} — ${status}`}
    >
      <div style={{ fontSize: 14, fontWeight: 800 }}>{room.roomShortName}</div>
      <div style={{ fontSize: 9, marginTop: 2, opacity: 0.85, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
        {room.roomType}
      </div>
      <div style={{ fontSize: 10, marginTop: 1 }}>{status}</div>
    </button>
  )
}